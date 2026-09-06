using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class StudentService(AppDbContext db, UserManager<User> userManager)
    {
        public const string RoleName = "Estudiante";

        public record CreateStudentRequest(
        string DocumentType, string DocumentNumber, string FirstName, string LastName,
        string Email, string? PhoneNumber, string Password, int CampusId,
        string StudentCode, DateTime BirthDate, char Gender);

        public record CreateStudentResult(Student Student, string? TemporaryPassword);

        public Task<List<Student>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Students.Include(s => s.User).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Student?> GetByIdAsync(int id) =>
            db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.StudentId == id);

        public async Task<CreateStudentResult> CreateAsync(CreateStudentRequest request)
        {
            var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };
            if (!validDocTypes.Contains(request.DocumentType))
                throw new InvalidOperationException($"Tipo de documento '{request.DocumentType}' no válido.");

            if (request.Gender != 'M' && request.Gender != 'F' && request.Gender != 'O')
                throw new InvalidOperationException("Género no válido. Use: M, F, O.");

            if (request.BirthDate.Date > DateTime.Today)
                throw new InvalidOperationException("La fecha de nacimiento no puede ser futura.");

            var codeExists = await db.Students.AnyAsync(s => s.StudentCode == request.StudentCode);
            if (codeExists)
                throw new InvalidOperationException($"El código de estudiante '{request.StudentCode}' ya existe.");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidOperationException("La contraseña es obligatoria.");

            var existingUser = await db.Users.FirstOrDefaultAsync(u =>
                u.DocumentType == request.DocumentType && u.DocumentNumber == request.DocumentNumber);

            User user;

            if (existingUser is not null)
            {
                var alreadyStudent = await db.Students.AnyAsync(s => s.StudentId == existingUser.Id);
                if (alreadyStudent)
                    throw new InvalidOperationException("Este usuario ya está registrado como estudiante.");

                user = existingUser;
            }
            else
            {
                var campusExists = await db.Campuses.AnyAsync(c => c.CampusId == request.CampusId);
                if (!campusExists)
                    throw new InvalidOperationException($"La sede con id {request.CampusId} no existe.");

                user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    DocumentType = request.DocumentType,
                    DocumentNumber = request.DocumentNumber,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    CampusId = request.CampusId,
                    MustChangePassword = true,
                };

                var createResult = await userManager.CreateAsync(user, request.Password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }

            var alreadyInRole = await userManager.IsInRoleAsync(user, RoleName);
            if (!alreadyInRole)
            {
                var roleResult = await userManager.AddToRoleAsync(user, RoleName);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            var student = new Student
            {
                StudentId = user.Id,
                StudentCode = request.StudentCode,
                BirthDate = DateTime.SpecifyKind(request.BirthDate, DateTimeKind.Utc),
                Gender = request.Gender,
            };

            db.Students.Add(student);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
            }

            return new CreateStudentResult(student, null);
        }

        public async Task<(int Imported, List<string> Errors)> ImportFromCsvAsync(Stream csvStream)
        {
            var errors = new List<string>();
            int imported = 0;
            var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };

            var rawLines = new List<(int LineNumber, string Line)>();
            using (var reader = new StreamReader(csvStream))
            {
                await reader.ReadLineAsync();
                int lineNumber = 1;
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    lineNumber++;
                    if (!string.IsNullOrWhiteSpace(line))
                        rawLines.Add((lineNumber, line));
                }
            }

            var existingDocs = (await db.Users
                    .Select(u => u.DocumentType + "|" + u.DocumentNumber)
                    .ToListAsync())
                .ToHashSet();

            var existingEmails = (await db.Users
                    .Select(u => u.Email!.ToUpper())
                    .ToListAsync())
                .ToHashSet();

            var existingStudentCodes = (await db.Students
                    .Select(s => s.StudentCode)
                    .ToListAsync())
                .ToHashSet();

            var validCampusIds = (await db.Campuses
                    .Select(c => c.CampusId)
                    .ToListAsync())
                .ToHashSet();

            foreach (var (lineNumber, line) in rawLines)
            {
                var cols = line.Split(',');
                if (cols.Length < 10)
                {
                    errors.Add($"Línea {lineNumber}: columnas insuficientes (se esperan 10).");
                    continue;
                }

                var documentType = cols[0].Trim().ToUpper();
                var documentNumber = cols[1].Trim();
                var firstName = cols[2].Trim();
                var lastName = cols[3].Trim();
                var email = cols[4].Trim();
                var password = cols[5].Trim();
                var campusCode = cols[6].Trim();
                var studentCode = cols[7].Trim();
                var birthDateRaw = cols[8].Trim();
                var gender = cols[9].Trim().ToUpper();

                if (string.IsNullOrEmpty(documentType) || string.IsNullOrEmpty(documentNumber) ||
                    string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(campusCode) || string.IsNullOrEmpty(studentCode) ||
                    string.IsNullOrEmpty(birthDateRaw) || string.IsNullOrEmpty(gender))
                {
                    errors.Add($"Línea {lineNumber}: todos los campos son obligatorios.");
                    continue;
                }

                if (!validDocTypes.Contains(documentType))
                {
                    errors.Add($"Línea {lineNumber}: TipoDocumento '{documentType}' no válido.");
                    continue;
                }

                if (gender != "M" && gender != "F" && gender != "O")
                {
                    errors.Add($"Línea {lineNumber}: Genero '{gender}' no válido. Use: M, F, O.");
                    continue;
                }

                if (!DateTime.TryParse(birthDateRaw, out DateTime birthDate) || birthDate.Date > DateTime.Today)
                {
                    errors.Add($"Línea {lineNumber}: FechaNacimiento '{birthDateRaw}' no válida o es futura.");
                    continue;
                }

                var docKey = $"{documentType}|{documentNumber}";
                if (existingDocs.Contains(docKey))
                {
                    errors.Add($"Línea {lineNumber}: ya existe un usuario con documento {documentType} {documentNumber}.");
                    continue;
                }

                var emailKey = email.ToUpper();
                if (existingEmails.Contains(emailKey))
                {
                    errors.Add($"Línea {lineNumber}: el correo '{email}' ya está registrado.");
                    continue;
                }

                if (existingStudentCodes.Contains(studentCode))
                {
                    errors.Add($"Línea {lineNumber}: el código '{studentCode}' ya está registrado.");
                    continue;
                }

                if (!int.TryParse(campusCode, out int campusId) || !validCampusIds.Contains(campusId))
                {
                    errors.Add($"Línea {lineNumber}: campus '{campusCode}' no válido o no existe.");
                    continue;
                }

                var user = new User
                {
                    UserName = email,
                    Email = email,
                    DocumentType = documentType,
                    DocumentNumber = documentNumber,
                    FirstName = firstName,
                    LastName = lastName,
                    CampusId = campusId,
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    var msg = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    errors.Add($"Línea {lineNumber}: error al crear usuario — {msg}");
                    continue;
                }

                await userManager.AddToRoleAsync(user, RoleName);

                var student = new Student
                {
                    StudentId = user.Id,
                    StudentCode = studentCode,
                    BirthDate = birthDate,
                    Gender = gender[0],
                };

                db.Students.Add(student);
                try
                {
                    await db.SaveChangesAsync();
                    imported++;

                    existingDocs.Add(docKey);
                    existingEmails.Add(emailKey);
                    existingStudentCodes.Add(studentCode);
                }
                catch (DbUpdateException ex)
                {
                    db.ChangeTracker.Clear();
                    errors.Add($"Línea {lineNumber}: error al guardar Student — {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return (imported, errors);
        }

    }
}