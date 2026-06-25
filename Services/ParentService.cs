using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ParentService(AppDbContext db, UserManager<User> userManager)
    {
        public Task<List<Parent>> GetAllAsync() =>
            db.Parents.Include(p => p.User).ToListAsync();

        public async Task<Parent?> GetByIdAsync(int id) =>
            await db.Parents.Include(p => p.User).FirstOrDefaultAsync(p => p.ParentId == id);

        public async Task<Parent> CreateAsync(Parent parent)
        {
            db.Parents.Add(parent);
            await db.SaveChangesAsync();
            return parent;
        }

        public async Task<bool> UpdateAsync(int id, Parent updated)
        {
            var parent = await db.Parents.FindAsync(id);
            if (parent is null) return false;

            parent.Relationship = updated.Relationship;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var parent = await db.Parents.FindAsync(id);
            if (parent is null) return false;

            db.Parents.Remove(parent);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<(int Imported, List<string> Errors)> ImportFromCsvAsync(Stream csvStream)
        {
            var errors = new List<string>();
            int imported = 0;

            var validDocTypes = new[] { "CC", "TI", "CE", "PAS", "RC" };
            var validRelationships = new[] { "FATHER", "MOTHER", "GRANDFATHER", "GRANDMOTHER",
                                             "UNCLE", "AUNT", "BROTHER", "SISTER", "OTHER" };

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
                    errors.Add($"Línea {lineNumber}: columnas insuficientes (se esperan al menos 10).");
                    continue;
                }

                var documentType = cols[0].Trim().ToUpper();
                var documentNumber = cols[1].Trim();
                var firstName = cols[2].Trim();
                var lastName = cols[3].Trim();
                var email = cols[4].Trim();
                var password = cols[5].Trim();
                var campusIdRaw = cols[6].Trim();
                var relationship = cols[7].Trim().ToUpper();
                var primaryContactRaw = cols[8].Trim();
                var studentDocNumbersRaw = cols[9].Trim();

                if (string.IsNullOrEmpty(documentType) || string.IsNullOrEmpty(documentNumber) ||
                    string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(campusIdRaw) || string.IsNullOrEmpty(relationship) ||
                    string.IsNullOrEmpty(primaryContactRaw) || string.IsNullOrEmpty(studentDocNumbersRaw))
                {
                    errors.Add($"Línea {lineNumber}: todos los campos son obligatorios.");
                    continue;
                }

                if (!validDocTypes.Contains(documentType))
                {
                    errors.Add($"Línea {lineNumber}: Tipo de documento '{documentType}' no válido.");
                    continue;
                }

                if (!validRelationships.Contains(relationship))
                {
                    errors.Add($"Línea {lineNumber}: Parentesco '{relationship}' no válido.");
                    continue;
                }

                if (!int.TryParse(campusIdRaw, out int campusId) ||
                    !await db.Campuses.AnyAsync(c => c.CampusId == campusId))
                {
                    errors.Add($"Línea {lineNumber}: campus '{campusIdRaw}' no válido o no existe.");
                    continue;
                }

                if (primaryContactRaw != "0" && primaryContactRaw != "1")
                {
                    errors.Add($"Línea {lineNumber}: Contacto primario debe ser 0 o 1.");
                    continue;
                }
                bool primaryContact = primaryContactRaw == "1";

                if (await db.Users.AnyAsync(u => u.DocumentType == documentType && u.DocumentNumber == documentNumber))
                {
                    errors.Add($"Línea {lineNumber}: ya existe un usuario con documento {documentType} {documentNumber}.");
                    continue;
                }

                if (await userManager.FindByEmailAsync(email) is not null)
                {
                    errors.Add($"Línea {lineNumber}: el correo '{email}' ya está registrado.");
                    continue;
                }

                // Buscar estudiantes por tabla Student, sin depender de Role
                var studentDocNumbers = studentDocNumbersRaw.Split('-');
                var studentIds = new List<int>();
                bool studentsValid = true;

                foreach (var docNum in studentDocNumbers)
                {
                    var doc = docNum.Trim();
                    var studentId = await db.Students
                        .Where(s => s.User.DocumentNumber == doc)
                        .Select(s => s.StudentId)
                        .FirstOrDefaultAsync();

                    if (studentId == 0)
                    {
                        errors.Add($"Línea {lineNumber}: no existe un estudiante con documento '{doc}'.");
                        studentsValid = false;
                        break;
                    }

                    studentIds.Add(studentId);
                }

                if (!studentsValid) continue;

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

                await userManager.AddToRoleAsync(user, "PARENT");

                var parent = new Parent
                {
                    ParentId = user.Id,
                    Relationship = relationship,
                };

                db.Parents.Add(parent);
                try
                {
                    await db.SaveChangesAsync();

                    foreach (var studentId in studentIds)
                    {
                        db.ParentStudents.Add(new ParentStudent
                        {
                            ParentId = parent.ParentId,
                            StudentId = studentId,
                            PrimaryContact = primaryContact,
                        });
                    }

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