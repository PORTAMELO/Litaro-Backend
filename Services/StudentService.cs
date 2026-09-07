using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class StudentService(AppDbContext db, UserManager<User> userManager)
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

                await userManager.AddToRoleAsync(user, "STUDENT");

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