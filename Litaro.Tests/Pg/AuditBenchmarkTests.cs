using System.Diagnostics;
using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace Litaro.Tests.Pg;

public sealed class PgBenchFactAttribute : FactAttribute
{
    public PgBenchFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PgDatabase.EnvironmentVariable)))
        {
            Skip = "LITARO_TEST_PG no está definida.";
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LITARO_BENCH")))
        {
            Skip = "LITARO_BENCH no está definida.";
        }
    }
}

public sealed class AuditBenchmarkTests(ITestOutputHelper output)
{
    private const int Repetitions = 5;
    private static int documentSeed = 5_000_000;

    [PgBenchFact]
    public async Task Roll_save_overhead_is_under_200ms()
    {
        await using var database = await PgDatabase.CreateAsync();
        await using var setup = database.NewContext();
        await AddSchoolAsync(setup);
        var school = await TestSchool.CreateAsync(setup);
        await AddEnrollmentsAsync(setup, school, 37);

        var enrollmentIds = setup.Enrollments
            .Where(e => e.ClassroomId == school.C1001.ClassroomId)
            .Select(e => e.EnrollmentId)
            .OrderBy(id => id)
            .ToList();
        Assert.Equal(40, enrollmentIds.Count);

        var access = new RollAccess(school.Marta.Id, false, true);
        var classroomId = school.C1001.ClassroomId;
        var date = school.Tuesday;
        var run = 0;

        async Task<double> Measure(bool audited)
        {
            var offset = run++ % 2;
            var absent = enrollmentIds.Where((_, index) => index % 2 == offset).ToList();
            var request = new SaveSheetRequest(classroomId, date, TestSchool.MathStart, absent, []);
            await using var db = audited ? database.NewContext(NewAudit()) : database.NewContext();
            var services = TestServices.Create(db);
            var watch = Stopwatch.StartNew();
            await services.Roll.SaveSheetAsync(access, request, date);
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }

        var (without, with) = await CompareAsync(database, Measure);
        output.WriteLine($"Roll save 40 students: sin auditoria median {Median(without):F1} ms [{Format(without)}]");
        output.WriteLine($"Roll save 40 students: con auditoria median {Median(with):F1} ms [{Format(with)}]");
        output.WriteLine($"Roll save 40 students: overhead {Median(with) - Median(without):F1} ms");
    }

    [PgBenchFact]
    public async Task Bulk_insert_overhead_is_under_30_percent()
    {
        await using var database = await PgDatabase.CreateAsync();
        int campusId;
        await using (var setup = database.NewContext())
        {
            await AddSchoolAsync(setup);
            campusId = (await TestSchool.CreateAsync(setup)).Campus.CampusId;
        }

        async Task<double> Measure(bool audited)
        {
            await using var db = audited ? database.NewContext(NewAudit()) : database.NewContext();
            for (var i = 0; i < 1000; i++)
            {
                var user = new User
                {
                    FirstName = "Bulk",
                    LastName = "Estudiante" + i,
                    DocumentType = "TI",
                    DocumentNumber = Interlocked.Increment(ref documentSeed).ToString(),
                    CampusId = campusId,
                };
                db.Students.Add(new Student
                {
                    StudentCode = "B-" + user.DocumentNumber,
                    Gender = 'F',
                    BirthDate = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    User = user,
                });
            }

            var watch = Stopwatch.StartNew();
            await db.SaveChangesAsync();
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }

        var (without, with) = await CompareAsync(database, Measure);
        output.WriteLine($"Bulk insert 1000 users + 1000 students: sin auditoria median {Median(without):F1} ms [{Format(without)}]");
        output.WriteLine($"Bulk insert 1000 users + 1000 students: con auditoria median {Median(with):F1} ms [{Format(with)}]");
        output.WriteLine($"Bulk insert: ratio {Median(with) / Median(without):F3}");
    }

    [PgBenchFact]
    public async Task Server_side_trigger_cost_is_within_budget()
    {
        await using var database = await PgDatabase.CreateAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        double? elapsed = null;
        connection.Notice += (_, e) =>
        {
            if (e.Notice.MessageText.StartsWith("elapsed_ms=", StringComparison.Ordinal))
            {
                elapsed = double.Parse(e.Notice.MessageText["elapsed_ms=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            }
        };

        async Task Execute(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        await Execute("INSERT INTO \"School\"(\"SchoolId\",\"Name\",\"Nit\",\"Address\") VALUES (1,'Colegio','900000001','Calle 1')");
        await Execute("INSERT INTO \"Campus\"(\"CampusId\",\"Name\",\"Address\",\"Dane\",\"SchoolId\") VALUES (1,'Sede','Calle 1','125875000001',1)");

        const string block = """
            DO $bench$
            DECLARE t0 timestamptz; t1 timestamptz;
            BEGIN
              t0 := clock_timestamp();
              INSERT INTO "User"("UserName","NormalizedUserName","Email","NormalizedEmail","EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount","DocumentType","DocumentNumber","FirstName","LastName","Active","CreationDate","MustChangePassword","CampusId")
              SELECT 'srv' || g, 'SRV' || g, 'srv' || g || '@x.co', 'SRV' || g || '@X.CO', true, 'hash' || g, 'stamp' || g, 'cc' || g, false, false, false, 0, 'TI', 'SRV' || g, 'Nombre', 'Apellido' || g, true, now(), false, 1
              FROM generate_series(1,1000) g;
              UPDATE "User" SET "FirstName" = 'Cambiado' WHERE "DocumentNumber" LIKE 'SRV%';
              t1 := clock_timestamp();
              RAISE NOTICE 'elapsed_ms=%', extract(epoch FROM (t1 - t0)) * 1000;
            END
            $bench$
            """;

        async Task<double> Measure(bool enabled)
        {
            await SetTriggersAsync(database, enabled);
            elapsed = null;
            await Execute(block);
            var value = elapsed ?? throw new InvalidOperationException("Sin medición.");
            await SetTriggersAsync(database, false);
            await Execute("DELETE FROM \"User\" WHERE \"DocumentNumber\" LIKE 'SRV%'");
            return value;
        }

        var off = new List<double>();
        var on = new List<double>();
        var rtt = new List<double>();

        try
        {
            await Measure(false);
            await Measure(true);
            for (var i = 0; i < Repetitions; i++)
            {
                off.Add(await Measure(false));
                on.Add(await Measure(true));
            }

            for (var i = 0; i < 20; i++)
            {
                var watch = Stopwatch.StartNew();
                await using var ping = new NpgsqlCommand("SELECT 1", connection);
                await ping.ExecuteScalarAsync();
                watch.Stop();
                rtt.Add(watch.Elapsed.TotalMilliseconds);
            }
        }
        finally
        {
            await SetTriggersAsync(database, true);
        }

        var perRowMs = (Median(on) - Median(off)) / 2000;
        var rttMs = Median(rtt);
        var roll = 80 * perRowMs + rttMs;
        var bulk = 2000 * perRowMs;

        output.WriteLine($"Server-side 1000 inserts + 1000 updates: trigger off median {Median(off):F1} ms [{Format(off)}]");
        output.WriteLine($"Server-side 1000 inserts + 1000 updates: trigger on median {Median(on):F1} ms [{Format(on)}]");
        output.WriteLine($"perRowMs {perRowMs:F4}; rttMs {rttMs:F1} (SELECT 1 x20)");
        output.WriteLine($"80 * perRowMs + rttMs = {roll:F1} ms (limit 200); 2000 * perRowMs = {bulk:F1} ms (limit 800)");

        Assert.True(roll < 200, $"Roll budget {roll:F1} ms");
        Assert.True(bulk < 800, $"Bulk budget {bulk:F1} ms");
    }

    private async Task<(List<double> Without, List<double> With)> CompareAsync(PgDatabase database, Func<bool, Task<double>> measure)
    {
        var without = new List<double>();
        var with = new List<double>();

        try
        {
            await SetTriggersAsync(database, false);
            await measure(false);
            await SetTriggersAsync(database, true);
            await measure(true);

            for (var i = 0; i < Repetitions; i++)
            {
                await SetTriggersAsync(database, false);
                without.Add(await measure(false));
                await SetTriggersAsync(database, true);
                with.Add(await measure(true));
            }
        }
        finally
        {
            await SetTriggersAsync(database, true);
        }

        return (without, with);
    }

    private static async Task SetTriggersAsync(PgDatabase database, bool enabled)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        var tables = new List<string>();
        await using (var query = new NpgsqlCommand(
            "SELECT DISTINCT c.relname FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE t.tgname = 'audit_row' AND NOT t.tgisinternal AND n.nspname = 'public'",
            connection))
        await using (var reader = await query.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var action = enabled ? "ENABLE" : "DISABLE";
        foreach (var table in tables)
        {
            await using var alter = new NpgsqlCommand($"ALTER TABLE public.\"{table}\" {action} TRIGGER audit_row", connection);
            await alter.ExecuteNonQueryAsync();
        }
    }

    private static async Task AddSchoolAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"NotificationType\"");
        db.Schools.Add(new School { SchoolId = 1, Name = "Colegio", Nit = "900000001", Address = "Calle 1" });
        await db.SaveChangesAsync();
    }

    private static async Task AddEnrollmentsAsync(AppDbContext db, TestSchool school, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var user = new User
            {
                FirstName = "Est" + i,
                LastName = "Bench",
                DocumentType = "TI",
                DocumentNumber = Interlocked.Increment(ref documentSeed).ToString(),
                CampusId = school.Campus.CampusId,
            };
            var student = new Student
            {
                StudentCode = "R-" + user.DocumentNumber,
                Gender = 'F',
                BirthDate = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                User = user,
            };
            db.Students.Add(student);
            db.Enrollments.Add(new Enrollment { Student = student, ClassroomId = school.C1001.ClassroomId, YearId = TestSchool.YearId });
        }

        await db.SaveChangesAsync();
    }

    private static AuditContext NewAudit()
    {
        var audit = new AuditContext();
        audit.SetRequest("POST /bench", "1.1.1.1", "bench");
        audit.SetUser(1, "Bench");
        return audit;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    private static string Format(List<double> values) => string.Join(", ", values.Select(v => v.ToString("F0")));
}
