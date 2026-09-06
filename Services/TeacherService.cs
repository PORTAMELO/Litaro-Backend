using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class TeacherService(AppDbContext db, UserManager<User> userManager)
    {
        public const string RoleName = "Profesor";

        public record CreateTeacherRequest(
        string DocumentType, string DocumentNumber, string FirstName, string LastName,
        string Email, string? PhoneNumber, string Password, int CampusId, string Specialty);

        public record CreateTeacherResult(Teacher Teacher, string? TemporaryPassword);

        public Task<List<Teacher>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Teachers.Include(t => t.User).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Teacher?> GetByIdAsync(int id) =>
            db.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.TeacherId == id);

        public async Task<CreateTeacherResult> CreateAsync(CreateTeacherRequest request)
        {
            var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };
            if (!validDocTypes.Contains(request.DocumentType))
                throw new InvalidOperationException($"Tipo de documento '{request.DocumentType}' no válido.");

            if (string.IsNullOrWhiteSpace(request.Specialty))
                throw new InvalidOperationException("La especialidad es obligatoria.");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidOperationException("La contraseña es obligatoria.");

            var existingUser = await db.Users.FirstOrDefaultAsync(u =>
                u.DocumentType == request.DocumentType && u.DocumentNumber == request.DocumentNumber);

            User user;

            if (existingUser is not null)
            {
                var alreadyTeacher = await db.Teachers.AnyAsync(t => t.TeacherId == existingUser.Id);
                if (alreadyTeacher)
                    throw new InvalidOperationException("Este usuario ya está registrado como docente.");

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

            var teacher = new Teacher
            {
                TeacherId = user.Id,
                Specialty = request.Specialty.Trim(),
            };

            db.Teachers.Add(teacher);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
            }

            return new CreateTeacherResult(teacher, null);
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

            var validCampusIds = (await db.Campuses
                    .Select(c => c.CampusId)
                    .ToListAsync())
                .ToHashSet();

            foreach (var (lineNumber, line) in rawLines)
            {
                var cols = line.Split(',');
                if (cols.Length < 8)
                {
                    errors.Add($"Línea {lineNumber}: columnas insuficientes (se esperan 8).");
                    continue;
                }

                var documentType = cols[0].Trim().ToUpper();
                var documentNumber = cols[1].Trim();
                var firstName = cols[2].Trim();
                var lastName = cols[3].Trim();
                var email = cols[4].Trim();
                var password = cols[5].Trim();
                var campusCode = cols[6].Trim();
                var specialty = cols[7].Trim();

                if (string.IsNullOrEmpty(documentType) || string.IsNullOrEmpty(documentNumber) ||
                    string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(campusCode) || string.IsNullOrEmpty(specialty))
                {
                    errors.Add($"Línea {lineNumber}: todos los campos son obligatorios.");
                    continue;
                }

                if (!validDocTypes.Contains(documentType))
                {
                    errors.Add($"Línea {lineNumber}: TipoDocumento '{documentType}' no válido.");
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

                var teacher = new Teacher
                {
                    TeacherId = user.Id,
                    Specialty = specialty,
                };

                db.Teachers.Add(teacher);
                try
                {
                    await db.SaveChangesAsync();
                    imported++;

                    existingDocs.Add(docKey);
                    existingEmails.Add(emailKey);
                }
                catch (DbUpdateException ex)
                {
                    db.ChangeTracker.Clear();
                    errors.Add($"Línea {lineNumber}: error al guardar Teacher — {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return (imported, errors);
        }
    }
}