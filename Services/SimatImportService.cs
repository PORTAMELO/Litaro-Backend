using System.Globalization;
using ClosedXML.Excel;
using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SimatRowIssue(int Row, string Severity, string Document, string Name, string Message);

public record SimatMissingStudent(int StudentId, string Document, string Name);

public class SimatImportResult
{
    public bool DryRun { get; set; }
    public bool Applied { get; set; }
    public int TotalRows { get; set; }
    public int StudentsCreated { get; set; }
    public int StudentsUpdated { get; set; }
    public int EnrollmentsCreated { get; set; }
    public int EnrollmentsUpdated { get; set; }
    public int ClassroomsCreated { get; set; }
    public int RowsSkipped { get; set; }
    public int AlertsCreated { get; set; }
    public int RowsWithIssues => Issues.Count(i => i.Severity != SimatImportService.Warning);
    public List<string> GlobalErrors { get; } = [];
    public List<SimatRowIssue> Issues { get; } = [];
    public List<SimatMissingStudent> MissingInSimat { get; } = [];
}

public class SimatImportService(AppDbContext db, UserManager<User> userManager, SimatAlertService alerts)
{
    public const string Error = "Error";
    public const string Conflict = "Conflicto";
    public const string Warning = "Aviso";

    private const string StudentRoleName = "Estudiante";
    private const string Enrolled = "MATRICULADO";
    private static readonly string[] RequiredColumns =
    [
        "ANO", "DANE", "ESTADO", "MOTIVO", "FECHAINI",
        "CODIGO_DANE_SEDE", "JORNADA", "GRADO_COD", "GRUPO",
        "PER_ID", "NUI", "TIPODOC", "DOC",
        "APELLIDO1", "APELLIDO2", "NOMBRE1", "NOMBRE2", "GENERO", "FECHA_NACIMIENTO",
        "TELEFONO", "BARRIO", "EPS", "TIPO DE SANGRE", "DISCAPACIDAD", "TRA_ESP_APR_ESCOLAR"
    ];

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy", "d/MM/yyyy", "d/M/yyyy"
    ];

    private static string? MapStatus(string simatState) => simatState switch
    {
        "MATRICULADO" => "ACTIVE",
        "RETIRADO" => "WITHDRAWN",
        "TRASLADADO" => "TRANSFERRED",
        "GRADUADO" => "GRADUATED",
        _ => null
    };

    private sealed record SimatRow(
        int RowNumber, int? Year, string Dane, string State, string? Reason, DateTime? StartDate,
        string CampusDane, string? Shift, short? GradeCode, string Group,
        long? PerId, string Nui, string DocType, string DocNumber,
        string FirstName, string LastName, char? Gender, DateTime? BirthDate,
        string? Phone, string? Neighborhood, string? Eps, string? BloodType, bool HasPiar)
    {
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string Document => $"{DocType} {DocNumber}".Trim();
    }

    private sealed class RowPlan
    {
        public required SimatRow Row { get; init; }
        public required string Status { get; init; }
        public bool StatusOnly { get; init; }
        public User? User { get; set; }
        public bool CreateUser { get; set; }
        public Student? Student { get; set; }
        public string? NewStudentCode { get; set; }
        public bool AddRole { get; set; }
        public Campus? Campus { get; set; }
        public Classroom? Classroom { get; set; }
        public Enrollment? Enrollment { get; set; }
    }

    public async Task<SimatImportResult> ImportAsync(Stream input, short yearId, bool dryRun)
    {
        var result = new SimatImportResult { DryRun = dryRun };

        List<SimatRow> rows;
        try
        {
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer);
            buffer.Position = 0;
            rows = ReadRows(buffer, result);
        }
        catch (Exception ex)
        {
            result.GlobalErrors.Add($"No se pudo leer el archivo. ¿Es el Excel (.xlsx) descargado del SIMAT? Detalle: {ex.Message}");
            return result;
        }

        if (result.GlobalErrors.Count > 0)
            return result;

        result.TotalRows = rows.Count;
        if (rows.Count == 0)
        {
            result.GlobalErrors.Add("El archivo no tiene filas de estudiantes.");
            return result;
        }

        var year = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.YearId == yearId);
        if (year is null)
        {
            result.GlobalErrors.Add($"No existe el año académico {yearId} en LITARO.");
            return result;
        }

        var fileYears = rows.Select(r => r.Year).Distinct().ToList();
        if (fileYears.Count != 1 || fileYears[0] != year.StartDate.Year)
        {
            var years = string.Join(", ", fileYears.Select(y => y?.ToString() ?? "(vacío)"));
            result.GlobalErrors.Add($"El archivo es del año {years}, pero el año académico seleccionado empieza en {year.StartDate.Year}.");
            return result;
        }

        var campuses = await db.Campuses.AsNoTracking().ToListAsync();
        var campusByDane = campuses
            .Where(c => !string.IsNullOrWhiteSpace(c.Dane))
            .GroupBy(c => c.Dane.Trim())
            .ToDictionary(g => g.Key, g => g.First());

        var fileDanes = rows.Select(r => r.Dane).Distinct().ToList();
        if (fileDanes.Count != 1 || !campusByDane.ContainsKey(fileDanes[0]))
        {
            result.GlobalErrors.Add(
                $"El archivo es de la institución con código DANE {string.Join(", ", fileDanes)}, " +
                "que no coincide con el campo Dane de ninguna sede de LITARO. Revise la configuración de Sedes.");
            return result;
        }

        var roleId = await db.Roles
            .Where(r => r.NormalizedName == StudentRoleName.ToUpperInvariant())
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();
        if (roleId is null)
        {
            result.GlobalErrors.Add($"No existe el rol '{StudentRoleName}' en LITARO.");
            return result;
        }

        var gradeByCode = (await db.Grades.AsNoTracking().Where(g => g.SimatCode != null).ToListAsync())
            .ToDictionary(g => g.SimatCode!.Value);
        var classrooms = await db.Classrooms.ToListAsync();
        var users = await db.Users.ToListAsync();
        var students = await db.Students.ToListAsync();
        var enrollments = await db.Enrollments.Where(e => e.YearId == yearId).ToListAsync();
        var usersWithRole = (await db.UserRoles
                .Where(ur => ur.RoleId == roleId.Value)
                .Select(ur => ur.UserId)
                .ToListAsync())
            .ToHashSet();

        var userById = users.ToDictionary(u => u.Id);
        var usersByDocument = users.ToLookup(u => u.DocumentNumber.Trim());
        var studentById = students.ToDictionary(s => s.StudentId);
        var studentByPerId = students
            .Where(s => s.SimatPerId != null)
            .ToDictionary(s => s.SimatPerId!.Value);
        var enrollmentByStudent = enrollments.ToDictionary(e => e.StudentId);
        var takenUserNames = users
            .Where(u => u.NormalizedUserName != null)
            .Select(u => u.NormalizedUserName!)
            .ToHashSet();
        var takenStudentCodes = students
            .Select(s => s.StudentCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = new List<SimatRow>();
        foreach (var group in rows.GroupBy(r => r.PerId))
        {
            if (group.Key is null)
            {
                foreach (var r in group)
                    AddIssue(result, r, Error, "La fila no tiene PER_ID.");
                continue;
            }

            var ordered = group
                .OrderByDescending(r => r.State == Enrolled)
                .ThenByDescending(r => r.StartDate ?? DateTime.MinValue)
                .ToList();

            selected.Add(ordered[0]);
            foreach (var duplicate in ordered.Skip(1))
            {
                result.RowsSkipped++;
                AddIssue(result, duplicate, Warning,
                    $"La persona aparece varias veces en el archivo; se tomó la fila {ordered[0].RowNumber}.");
            }
        }

        var repeatedDocuments = selected
            .Where(r => r.DocNumber.Length > 0)
            .GroupBy(r => r.DocNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var plans = new List<RowPlan>();
        var plannedUserIds = new HashSet<int>();
        var newClassrooms = new List<Classroom>();
        var classroomShiftUpdates = new Dictionary<Classroom, string>();

        foreach (var r in selected.OrderBy(r => r.RowNumber))
        {
            var status = MapStatus(r.State);
            if (status is null)
            {
                AddIssue(result, r, Error, $"Estado SIMAT '{r.State}' no reconocido.");
                continue;
            }

            if (r.DocNumber.Length == 0)
            {
                AddIssue(result, r, Error, "La fila no tiene número de documento.");
                continue;
            }

            if (repeatedDocuments.Contains(r.DocNumber))
            {
                AddIssue(result, r, Conflict,
                    $"El documento {r.DocNumber} aparece con más de un PER_ID en el archivo. Revíselo en el SIMAT.");
                continue;
            }

            var documentMatches = usersByDocument[r.DocNumber].ToList();
            var student = studentByPerId.GetValueOrDefault(r.PerId!.Value);
            User? user = null;

            if (student is not null)
            {
                user = userById[student.StudentId];
                var other = documentMatches.FirstOrDefault(u => u.Id != student.StudentId);
                if (other is not null)
                {
                    AddIssue(result, r, Conflict,
                        $"El PER_ID {r.PerId} es de {Name(user)}, pero el documento {r.DocNumber} está registrado " +
                        $"a nombre de {Name(other)}. Revise cuál es el correcto.");
                    continue;
                }
            }
            else if (documentMatches.Count > 1)
            {
                AddIssue(result, r, Conflict,
                    $"Hay {documentMatches.Count} usuarios en LITARO con el número de documento {r.DocNumber}.");
                continue;
            }
            else if (documentMatches.Count == 1)
            {
                user = documentMatches[0];
                student = studentById.GetValueOrDefault(user.Id);

                if (student?.SimatPerId is not null && student.SimatPerId != r.PerId)
                {
                    AddIssue(result, r, Conflict,
                        $"El documento {r.DocNumber} es de un estudiante que en LITARO tiene otro PER_ID " +
                        $"({student.SimatPerId}). Puede ser un registro duplicado en el SIMAT.");
                    continue;
                }

                if (student is null && !string.Equals(user.DocumentType, r.DocType, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(result, r, Conflict,
                        $"El número {r.DocNumber} ya existe en LITARO como {user.DocumentType} a nombre de " +
                        $"{Name(user)}, que no es estudiante. Verifique que sea la misma persona.");
                    continue;
                }
            }

            if (user is not null && plannedUserIds.Contains(user.Id))
            {
                AddIssue(result, r, Conflict, $"Otra fila del archivo ya corresponde a {Name(user)} en LITARO.");
                continue;
            }

            if (r.State != Enrolled)
            {
                if (student is null || !enrollmentByStudent.TryGetValue(student.StudentId, out var current))
                {
                    result.RowsSkipped++;
                    continue;
                }

                plans.Add(new RowPlan
                {
                    Row = r,
                    Status = status,
                    StatusOnly = true,
                    User = user,
                    Student = student,
                    Enrollment = current
                });
                plannedUserIds.Add(student.StudentId);
                result.EnrollmentsUpdated++;
                continue;
            }

            var problem = ValidateEnrolledRow(r);
            if (problem is not null)
            {
                AddIssue(result, r, Error, problem);
                continue;
            }

            if (!campusByDane.TryGetValue(r.CampusDane, out var campus))
            {
                AddIssue(result, r, Error,
                    $"La sede con código DANE {r.CampusDane} no está registrada en LITARO (campo Dane de Sedes).");
                continue;
            }

            if (!gradeByCode.TryGetValue(r.GradeCode!.Value, out var grade))
            {
                AddIssue(result, r, Error,
                    $"El grado SIMAT {r.GradeCode} no está asociado a ningún grado de LITARO (campo SimatCode de Grados).");
                continue;
            }

            var classroom = PlanClassroom(classrooms, newClassrooms, classroomShiftUpdates, campus, grade, r, result);

            var plan = new RowPlan
            {
                Row = r,
                Status = status,
                User = user,
                Student = student,
                Campus = campus,
                Classroom = classroom
            };

            if (user is null)
            {
                plan.User = BuildNewUser(r, campus, takenUserNames);
                plan.CreateUser = true;
                plan.AddRole = true;
            }
            else
            {
                plan.AddRole = !usersWithRole.Contains(user.Id);
                plannedUserIds.Add(user.Id);
            }

            if (student is null)
            {
                plan.NewStudentCode = PickStudentCode(r, takenStudentCodes);
                result.StudentsCreated++;
            }
            else
            {
                result.StudentsUpdated++;
            }

            if (student is not null && enrollmentByStudent.TryGetValue(student.StudentId, out var enrollment))
            {
                plan.Enrollment = enrollment;
                result.EnrollmentsUpdated++;
            }
            else
            {
                result.EnrollmentsCreated++;
            }

            plans.Add(plan);
        }

        var filePerIds = rows.Where(r => r.PerId is not null).Select(r => r.PerId!.Value).ToHashSet();
        var fileDocuments = rows.Where(r => r.DocNumber.Length > 0).Select(r => r.DocNumber).ToHashSet();

        foreach (var litaroEnrollment in enrollments.Where(e => e.Status == "ACTIVE"))
        {
            if (!studentById.TryGetValue(litaroEnrollment.StudentId, out var litaroStudent))
                continue;

            userById.TryGetValue(litaroStudent.StudentId, out var litaroUser);

            var inFile = (litaroStudent.SimatPerId is { } knownPerId && filePerIds.Contains(knownPerId))
                         || (litaroUser is not null && fileDocuments.Contains(litaroUser.DocumentNumber.Trim()));

            if (!inFile)
            {
                result.MissingInSimat.Add(new SimatMissingStudent(
                    litaroStudent.StudentId,
                    litaroUser?.DocumentNumber ?? string.Empty,
                    Name(litaroUser)));
            }
        }

        if (dryRun || plans.Count == 0)
            return result;

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var (classroom, shift) in classroomShiftUpdates)
                classroom.Shift = shift;

            db.Classrooms.AddRange(newClassrooms);
            db.Users.AddRange(plans.Where(p => p.CreateUser).Select(p => p.User!));
            await db.SaveChangesAsync();

            foreach (var plan in plans)
                ApplyPlan(plan, yearId, roleId.Value);

            result.AlertsCreated = await QueueMissingAlertsAsync(result, yearId);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            result.Applied = true;
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            result.GlobalErrors.Add(
                "No se guardó ningún cambio (se deshizo toda la importación). Error de base de datos: " +
                (ex.InnerException?.Message ?? ex.Message));
        }

        return result;
    }

    private async Task<int> QueueMissingAlertsAsync(SimatImportResult result, short yearId)
    {
        if (result.MissingInSimat.Count == 0)
            return 0;

        var pendingStudentIds = await db.SimatAlerts
            .Where(a => a.Type == SimatAlertTypes.NotInSimat
                        && a.Status == SimatAlertStatuses.Pending
                        && a.YearId == yearId)
            .Select(a => a.StudentId)
            .ToListAsync();

        var alreadyPending = pendingStudentIds.OfType<int>().ToHashSet();
        var created = 0;

        foreach (var missing in result.MissingInSimat)
        {
            if (alreadyPending.Contains(missing.StudentId))
                continue;

            alerts.Add(
                SimatAlertTypes.NotInSimat,
                $"{missing.Name} ({missing.Document}) está matriculado en LITARO {yearId} pero no aparece en el archivo " +
                "del SIMAT. Verifique en el SIMAT y regístrelo o corríjalo a mano.",
                yearId,
                missing.StudentId);
            created++;
        }

        return created;
    }

    private void ApplyPlan(RowPlan plan, short yearId, int roleId)
    {
        var r = plan.Row;

        if (plan.StatusOnly)
        {
            plan.Enrollment!.Status = plan.Status;
            plan.Enrollment.StatusReason = r.Reason;
            return;
        }

        var user = plan.User!;

        if (!plan.CreateUser)
        {
            user.DocumentType = r.DocType;
            user.DocumentNumber = r.DocNumber;
            user.FirstName = r.FirstName;
            user.LastName = r.LastName;
            user.CampusId = plan.Campus!.CampusId;
            if (string.IsNullOrWhiteSpace(user.PhoneNumber) && r.Phone is not null)
                user.PhoneNumber = r.Phone;
        }

        var isNewStudent = plan.Student is null;
        var student = plan.Student ?? new Student { StudentId = user.Id, StudentCode = plan.NewStudentCode! };

        student.SimatPerId = r.PerId;
        student.BirthDate = DateTime.SpecifyKind(r.BirthDate!.Value.Date, DateTimeKind.Utc);
        student.Gender = r.Gender!.Value;
        student.Neighborhood = r.Neighborhood;
        student.Eps = r.Eps;
        student.BloodType = r.BloodType;
        student.HasPiar = r.HasPiar;
        student.Active = true;

        if (isNewStudent)
            db.Students.Add(student);

        if (plan.AddRole)
            db.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = roleId });

        var classroomId = plan.Classroom!.ClassroomId;
        if (plan.Enrollment is null)
        {
            db.Enrollments.Add(new Enrollment
            {
                StudentId = student.StudentId,
                ClassroomId = classroomId,
                YearId = yearId,
                Status = plan.Status,
                StatusReason = r.Reason,
                EnrollmentDate = ColombiaToUtc(r.StartDate) ?? DateTime.UtcNow
            });
        }
        else
        {
            plan.Enrollment.ClassroomId = classroomId;
            plan.Enrollment.Status = plan.Status;
            plan.Enrollment.StatusReason = r.Reason;
        }
    }

    private static string? ValidateEnrolledRow(SimatRow r)
    {
        if (r.FirstName.Length == 0 || r.LastName.Length == 0) return "Faltan nombres o apellidos.";
        if (r.DocType.Length is 0 or > 5) return $"Tipo de documento '{r.DocType}' no válido.";
        if (r.DocNumber.Length > 20) return "El número de documento tiene más de 20 caracteres.";
        if (r.Gender is null) return "Género no reconocido (se espera MASCULINO o FEMENINO).";
        if (r.BirthDate is null) return "Fecha de nacimiento vacía o inválida.";
        if (r.GradeCode is null) return "GRADO_COD vacío o inválido.";
        if (r.Group.Length == 0) return "La fila no tiene GRUPO.";
        if (r.CampusDane.Length == 0) return "La fila no tiene CODIGO_DANE_SEDE.";
        return null;
    }

    private static Classroom PlanClassroom(
        List<Classroom> classrooms,
        List<Classroom> newClassrooms,
        Dictionary<Classroom, string> shiftUpdates,
        Campus campus,
        Grade grade,
        SimatRow r,
        SimatImportResult result)
    {
        string? ShiftOf(Classroom c) => c.Shift ?? shiftUpdates.GetValueOrDefault(c);

        var sameGroup = classrooms
            .Where(c => c.CampusId == campus.CampusId
                        && c.GradeId == grade.GradeId
                        && string.Equals(c.Name, r.Group, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var exact = sameGroup.FirstOrDefault(c => string.Equals(ShiftOf(c), r.Shift, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var withoutShift = sameGroup.FirstOrDefault(c => ShiftOf(c) is null);
        if (withoutShift is not null && r.Shift is not null)
        {
            shiftUpdates[withoutShift] = r.Shift;
            return withoutShift;
        }

        var created = new Classroom
        {
            Name = r.Group,
            GradeId = grade.GradeId,
            CampusId = campus.CampusId,
            Shift = r.Shift,
            Active = true
        };

        classrooms.Add(created);
        newClassrooms.Add(created);
        result.ClassroomsCreated++;
        return created;
    }

    private User BuildNewUser(SimatRow r, Campus campus, HashSet<string> takenUserNames)
    {
        string[] candidates = [r.DocNumber, $"{r.DocType}{r.DocNumber}", $"SIMAT{r.PerId}"];
        var userName = candidates.First(c => !takenUserNames.Contains(userManager.NormalizeName(c)!));
        var normalized = userManager.NormalizeName(userName)!;
        takenUserNames.Add(normalized);

        return new User
        {
            UserName = userName,
            NormalizedUserName = normalized,
            Email = null,
            DocumentType = r.DocType,
            DocumentNumber = r.DocNumber,
            FirstName = r.FirstName,
            LastName = r.LastName,
            PhoneNumber = r.Phone,
            CampusId = campus.CampusId,
            Active = true,
            MustChangePassword = true,
            CreationDate = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true
        };
    }

    private static string PickStudentCode(SimatRow r, HashSet<string> takenCodes)
    {
        string[] candidates = [r.Nui, r.DocNumber, $"SIMAT{r.PerId}", $"S{r.PerId}R{r.RowNumber}"];
        foreach (var candidate in candidates)
        {
            if (candidate.Length is > 0 and <= 20 && takenCodes.Add(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"No se pudo generar un código de estudiante para la fila {r.RowNumber}.");
    }

    private static void AddIssue(SimatImportResult result, SimatRow r, string severity, string message) =>
        result.Issues.Add(new SimatRowIssue(r.RowNumber, severity, r.Document, r.FullName, message));

    private static string Name(User? u) =>
        u is null ? "(sin nombre)" : $"{u.FirstName} {u.LastName}".Trim();

    private static DateTime? ColombiaToUtc(DateTime? local) =>
        local is null ? null : DateTime.SpecifyKind(local.Value.AddHours(5), DateTimeKind.Utc);


    private static List<SimatRow> ReadRows(Stream stream, SimatImportResult result)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var header = sheet.FirstRowUsed();
        if (header is null)
        {
            result.GlobalErrors.Add("El archivo está vacío.");
            return [];
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (name.Length > 0)
                columns.TryAdd(name, cell.Address.ColumnNumber);
        }

        var missing = RequiredColumns.Where(c => !columns.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            result.GlobalErrors.Add($"No parece el archivo plano del SIMAT: faltan las columnas {string.Join(", ", missing)}.");
            return [];
        }

        var headerNumber = header.RowNumber();
        var rows = new List<SimatRow>();

        foreach (var row in sheet.RowsUsed())
        {
            if (row.RowNumber() <= headerNumber)
                continue;

            IXLCell Cell(string column) => row.Cell(columns[column]);
            string Text(string column) => ReadText(Cell(column));

            rows.Add(new SimatRow(
                RowNumber: row.RowNumber(),
                Year: int.TryParse(Text("ANO"), out var year) ? year : (int?)null,
                Dane: Text("DANE"),
                State: Text("ESTADO").ToUpperInvariant(),
                Reason: NullIfEmpty(Cut(Text("MOTIVO"), 150)),
                StartDate: ReadDate(Cell("FECHAINI")),
                CampusDane: Text("CODIGO_DANE_SEDE"),
                Shift: NullIfEmpty(Cut(Text("JORNADA").ToUpperInvariant(), 20)),
                GradeCode: short.TryParse(Text("GRADO_COD"), out var gradeCode) ? gradeCode : (short?)null,
                Group: Cut(Text("GRUPO"), 10),
                PerId: long.TryParse(Text("PER_ID"), out var perId) ? perId : (long?)null,
                Nui: Text("NUI"),
                DocType: ParseDocumentType(Text("TIPODOC")),
                DocNumber: Text("DOC"),
                FirstName: Cut(JoinNames(Text("NOMBRE1"), Text("NOMBRE2")), 100),
                LastName: Cut(JoinNames(Text("APELLIDO1"), Text("APELLIDO2")), 100),
                Gender: ParseGender(Text("GENERO")),
                BirthDate: ReadDate(Cell("FECHA_NACIMIENTO")),
                Phone: ParsePhone(Text("TELEFONO")),
                Neighborhood: NullIfEmpty(Cut(Text("BARRIO"), 100)),
                Eps: NullIfEmpty(Cut(Text("EPS"), 100)),
                BloodType: NullIfEmpty(Cut(Text("TIPO DE SANGRE").Replace(" ", ""), 5)),
                HasPiar: Applies(Text("DISCAPACIDAD")) || Applies(Text("TRA_ESP_APR_ESCOLAR"))));
        }

        return rows;
    }

    private static string ReadText(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        var value = cell.Value;

        if (value.IsNumber)
            return value.GetNumber().ToString("0.##########", CultureInfo.InvariantCulture);

        return cell.GetString().Trim();
    }

    private static DateTime? ReadDate(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        var value = cell.Value;
        if (value.IsDateTime)
            return value.GetDateTime();

        if (value.IsNumber)
        {
            var serial = value.GetNumber();
            return serial is > 0 and < 2958466 ? DateTime.FromOADate(serial) : null;
        }

        return DateTime.TryParseExact(cell.GetString().Trim(), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string ParseDocumentType(string text) =>
        text.Split(':')[0].Trim().ToUpperInvariant();

    private static char? ParseGender(string text) => text.Trim().ToUpperInvariant() switch
    {
        "MASCULINO" or "M" => 'M',
        "FEMENINO" or "F" => 'F',
        _ => null
    };

    private static string? ParsePhone(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return digits.Length is >= 7 and <= 15 ? digits : null;
    }

    private static bool Applies(string text)
    {
        var value = text.Trim().Replace('_', ' ');
        return value.Length > 0 && !value.Equals("NO APLICA", StringComparison.OrdinalIgnoreCase);
    }

    private static string JoinNames(params string[] parts) =>
        string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));

    private static string Cut(string text, int max) =>
        text.Length <= max ? text : text[..max];

    private static string? NullIfEmpty(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : text;
}