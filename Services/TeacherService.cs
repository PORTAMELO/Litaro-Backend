using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class TeacherService(AppDbContext db, UserManager<User> userManager)
    {
        public Task<List<Teacher>> GetAllAsync() =>
            db.Teachers.Include(t => t.User).ToListAsync();

        public async Task<Teacher?> GetByIdAsync(int id) =>
            await db.Teachers.Include(t => t.User).FirstOrDefaultAsync(t => t.TeacherId == id);

        public async Task<Teacher> CreateAsync(Teacher teacher)
        {
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
            return teacher;
        }

        public async Task<bool> UpdateAsync(int id, Teacher updated)
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher is null) return false;

            teacher.Specialty = updated.Specialty;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher is null) return false;

            db.Teachers.Remove(teacher);
            await db.SaveChangesAsync();
            return true;
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

                await userManager.AddToRoleAsync(user, "TEACHER");

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