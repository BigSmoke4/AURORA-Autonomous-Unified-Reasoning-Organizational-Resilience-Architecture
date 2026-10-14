using Aurora.Modules.Graph.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aurora.Modules.Graph.Infrastructure;

public class GraphNodeConfig : IEntityTypeConfiguration<GraphNode>
{
    public void Configure(EntityTypeBuilder<GraphNode> b)
    {
        b.ToTable("GraphNodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.RevenueExposure).HasColumnType("decimal(18,2)");
        b.HasIndex(x => new { x.OrganizationId, x.NodeType });
        b.HasIndex(x => new { x.OrganizationId, x.EntityId });
    }
}

public class GraphEdgeConfig : IEntityTypeConfiguration<GraphEdge>
{
    public void Configure(EntityTypeBuilder<GraphEdge> b)
    {
        b.ToTable("GraphEdges");
        b.HasKey(x => x.Id);
        b.Property(x => x.Strength).HasColumnType("decimal(4,3)");
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.SourceNodeId);
        b.HasIndex(x => x.TargetNodeId);
        b.HasOne<GraphNode>().WithMany().HasForeignKey(x => x.SourceNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GraphNode>().WithMany().HasForeignKey(x => x.TargetNodeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GraphSnapshotRecordConfig : IEntityTypeConfiguration<GraphSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<GraphSnapshotRecord> b)
    {
        b.ToTable("GraphSnapshotRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.PayloadJson).IsRequired().HasColumnType("text");
        b.HasIndex(x => new { x.OrganizationId, x.CapturedAtUtc });
    }
}
