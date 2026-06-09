using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class StudentService(AppDbContext db)
    {
        public Task<List<Student>> GetAllAsync() =>
            db.Students.Include(s => s.User).ToListAsync();

        public async Task<Student?> GetByIdAsync(int id) =>
            await db.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.StudentId == id);

        public async Task<Student> CreateAsync(Student student)
        {
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return student;
        }

        public async Task<bool> UpdateAsync(int id, Student updated)
        {
            var student = await db.Students.FindAsync(id);
            if (student is null) return false;

            student.StudentCode = updated.StudentCode;
            student.BirthDate = updated.BirthDate;
            student.Gender = updated.Gender;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var student = await db.Students.FindAsync(id);
            if (student is null) return false;

            db.Students.Remove(student);
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

                var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };
                if (!validDocTypes.Contains(documentType))
                {
                    errors.Add($"Línea {lineNumber}: TipoDocumento '{documentType}' no válido. Use: CC, TI, CE, PAS, RC.");
                    continue;
                }

                if (gender != "M" && gender != "F" && gender != "O")
                {
                    errors.Add($"Línea {lineNumber}: Genero '{gender}' no válido. Use: M, F, O.");
                    continue;
                }

                if (!DateTime.TryParse(birthDateRaw, out DateTime birthDate))
                {
                    errors.Add($"Línea {lineNumber}: FechaNacimiento '{birthDateRaw}' no tiene un formato válido.");
                    continue;
                }

                if (birthDate.Date > DateTime.Today)
                {
                    errors.Add($"Línea {lineNumber}: FechaNacimiento no puede ser una fecha futura.");
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

                bool codeExists = await db.Students.AnyAsync(s => s.StudentCode == studentCode);
                if (codeExists)
                {
                    errors.Add($"Línea {lineNumber}: el código de estudiante '{studentCode}' ya está registrado.");
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
                    Role = "STUDENT",
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

                var student = new Student
                {
                    StudentId = user.UserId,
                    StudentCode = studentCode,
                    BirthDate = birthDate,
                    Gender = gender[0],
                };

                db.Students.Add(student);

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
