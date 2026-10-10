using System.Data.Common;
using Ayush360.Hms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ayush360.Hms.Api.Registration;

/// <summary>
/// Hands out the next number from hospital.number_sequences in ONE atomic statement, so two desks registering
/// at the same moment can never get the same token, visit, receipt, patient or MLC number.
/// Call inside the same transaction as the insert that uses the number (it joins the current transaction).
/// </summary>
public sealed class NumberSequenceService(HospitalDbContext db)
{
    private const string Sql = """
        INSERT INTO hospital.number_sequences (tenant_id, sequence_key, period, last_value, updated_at)
        VALUES (@tenant, @key, @period, 1, now())
        ON CONFLICT (tenant_id, sequence_key, period)
        DO UPDATE SET last_value = hospital.number_sequences.last_value + 1, updated_at = now()
        RETURNING last_value
        """;

    public async Task<long> NextAsync(long tenantId, string key, string period, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = Sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            AddParameter(command, "tenant", tenantId);
            AddParameter(command, "key", key);
            AddParameter(command, "period", period);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt64(result);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
