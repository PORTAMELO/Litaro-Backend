using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ParentService(AppDbContext db)
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
                    errors.Add($"Línea {lineNumber}: TipoDocumento '{documentType}' no válido. Use: CC, TI, CE, PAS, RC.");
                    continue;
                }

                if (!validRelationships.Contains(relationship))
                {
                    errors.Add($"Línea {lineNumber}: Parentesco '{relationship}' no válido.");
                    continue;
                }

                if (!int.TryParse(campusIdRaw, out int campusId))
                {
                    errors.Add($"Línea {lineNumber}: CampusId '{campusIdRaw}' no es un número válido.");
                    continue;
                }

                bool campusExists = await db.Campuses.AnyAsync(c => c.CampusId == campusId);
                if (!campusExists)
                {
                    errors.Add($"Línea {lineNumber}: no existe un campus con Id '{campusId}'.");
                    continue;
                }

                if (primaryContactRaw != "0" && primaryContactRaw != "1")
                {
                    errors.Add($"Línea {lineNumber}: ContactoPrimario debe ser 0 o 1.");
                    continue;
                }
                bool primaryContact = primaryContactRaw == "1";

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

                var studentDocNumbers = studentDocNumbersRaw.Split('-');
                var studentIds = new List<int>();
                bool studentsValid = true;

                foreach (var docNum in studentDocNumbers)
                {
                    var doc = docNum.Trim();

                    var studentUser = await db.Users
                        .FirstOrDefaultAsync(u => u.DocumentNumber == doc && u.Role == "STUDENT");

                    if (studentUser is null)
                    {
                        errors.Add($"Línea {lineNumber}: no existe un estudiante con documento '{doc}'.");
                        studentsValid = false;
                        break;
                    }

                    studentIds.Add(studentUser.UserId);
                }

                if (!studentsValid) continue;

                var user = new User
                {
                    DocumentType = documentType,
                    DocumentNumber = documentNumber,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = password,
                    Role = "PARENT",
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

                var parent = new Parent
                {
                    ParentId = user.UserId,
                    Relationship = relationship,
                };

                db.Parents.Add(parent);

                try
                {
                    await db.SaveChangesAsync();

                    foreach (var studentId in studentIds)
                    {
                        var parentStudent = new ParentStudent
                        {
                            ParentId = parent.ParentId,
                            StudentId = studentId,
                            PrimaryContact = primaryContact,
                        };
                        db.ParentStudents.Add(parentStudent);
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
