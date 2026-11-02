using Pgvector;

namespace FinDoc.Infrastructure.Persistence;

public class ChunkEmbedding
{
    public Guid ChunkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Vector Vector { get; set; } = new(Array.Empty<float>());
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
