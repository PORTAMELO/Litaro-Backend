using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class TeacherService(AppDbContext db)
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

            using var reader = new StreamReader(csvStream);

            await reader.ReadLineAsync();

            int lineNumber = 1;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

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

                var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };
                if (!validDocTypes.Contains(documentType))
                {
                    errors.Add($"Línea {lineNumber}: TipoDocumento '{documentType}' no válido. Use: CC, TI, CE, PAS, RC.");
                    continue;
                }

                bool docExists = await db.Users.AnyAsync(u => u.DocumentType == documentType
                                                           && u.DocumentNumber == documentNumber);
                if (docExists)
                {
                    errors.Add($"Línea {lineNumber}: ya existe un usuario con documento {documentType} {documentNumber}.");
                    continue;
                }

                bool emailExists = await db.Users.AnyAsync(u => u.Email == email);
                if (emailExists)
                {
                    errors.Add($"Línea {lineNumber}: el correo '{email}' ya está registrado.");
                    continue;
                }

                if (!int.TryParse(campusCode, out int campusId))
                {
                    errors.Add($"Línea {lineNumber}: CampusId '{campusCode}' no es un número válido.");
                    continue;
                }

                bool campusExists = await db.Campuses.AnyAsync(c => c.CampusId == campusId);
                if (!campusExists)
                {
                    errors.Add($"Línea {lineNumber}: no existe un campus con Id '{campusId}'.");
                    continue;
                }

                var user = new User
                {
                    DocumentType = documentType,
                    DocumentNumber = documentNumber,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = password,
                    Role = "TEACHER",
                    CampusId = campusId,
                };

                try
                {
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    db.ChangeTracker.Clear();
                    errors.Add($"Línea {lineNumber}: error al guardar User — {ex.InnerException?.Message ?? ex.Message}");
                    continue;
                }

                var teacher = new Teacher
                {
                    TeacherId = user.UserId,
                    Specialty = specialty,
                };

                db.Teachers.Add(teacher);

                try
                {
                    await db.SaveChangesAsync();
                    imported++;
                }
                catch (DbUpdateException ex)
                {
                    db.ChangeTracker.Clear();
                    errors.Add($"Línea {lineNumber}: error al guardar — {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            return (imported, errors);
        }

    }
}
