using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.BrainNodes.Models;
using Npgsql;
using Pgvector;

namespace Chronaiq.Infrastructure.Persistence.Repositories;

/// <summary>
/// pgvector-backed implementation of <see cref="IBrainNodeSearch"/>. Executes the cosine
/// distance operator (<c>&lt;=&gt;</c>) directly over an <see cref="NpgsqlDataSource"/> that has
/// the vector type mapping registered, rather than through EF LINQ — the value-converted
/// <c>float[]</c> property cannot express the operator in translatable LINQ, and going straight
/// to ADO keeps the query and its HNSW index usage explicit.
/// </summary>
public sealed class BrainNodeSearch(NpgsqlDataSource dataSource) : IBrainNodeSearch
{
    public async Task<IReadOnlyList<BrainNodeMatch>> SearchAsync(
        Guid userId,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Title", "SourceType", ("Embedding" <=> $1) AS "Distance"
            FROM "BrainNodes"
            WHERE "UserId" = $2 AND "Embedding" IS NOT NULL
            ORDER BY "Embedding" <=> $1
            LIMIT $3;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = new Vector(queryEmbedding) },
                new NpgsqlParameter { Value = userId },
                new NpgsqlParameter { Value = limit }
            }
        };

        var results = new List<BrainNodeMatch>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new BrainNodeMatch(
                Id: reader.GetGuid(0),
                Title: reader.GetString(1),
                SourceType: reader.GetString(2),
                Distance: reader.GetDouble(3)));
        }

        return results;
    }
}
