using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem;

public class EqualizeOperationsFactory<TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private readonly IEqualityComparer<TModel>? _metadataEqualityComparer;
    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _attributesEqualityComparer = new FileSystemNodeModelAttributesEqualityComparer<TId>();
    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _linkEqualityComparer = new FileSystemNodeModelLinkEqualityComparer<TId>();

    public EqualizeOperationsFactory(IEqualityComparer<TModel>? metadataEqualityComparer = null)
    {
        _metadataEqualityComparer = metadataEqualityComparer;
    }

    public IEnumerable<Operation<TModel>> Operations(TModel? currentNodeModel, TModel? incomingNodeModel)
    {
        if (incomingNodeModel == null && currentNodeModel == null)
        {
            yield break;
        }

        if (incomingNodeModel != null && currentNodeModel == null)
        {
            yield return new Operation<TModel>(
                OperationType.Create,
                incomingNodeModel);

            yield break;
        }

        if (incomingNodeModel == null && currentNodeModel != null)
        {
            yield return new Operation<TModel>(
                OperationType.Delete,
                new TModel()
                    .WithId(currentNodeModel.Id));

            yield break;
        }

        if (incomingNodeModel!.Type != currentNodeModel!.Type)
        {
            throw new InvalidOperationException("Source and target node types do not match");
        }

        var metadataUpdated = false;

        if (!_linkEqualityComparer.Equals(incomingNodeModel, currentNodeModel))
        {
            yield return new Operation<TModel>(
                OperationType.Move,
                new TModel()
                    .WithId(incomingNodeModel.Id)
                    .WithLinkFrom(incomingNodeModel)
                    .WithMetadataFrom(incomingNodeModel));

            // Move operation also updates metadata
            metadataUpdated = true;
        }

        if (!_attributesEqualityComparer.Equals(incomingNodeModel, currentNodeModel))
        {
            yield return new Operation<TModel>(
                OperationType.Edit,
                new TModel()
                    .WithType<TModel, TId>(incomingNodeModel.Type)
                    .WithId(incomingNodeModel.Id)
                    .WithAttributesFrom(incomingNodeModel)
                    .WithMetadataFrom(incomingNodeModel));

            // Edit operation also updates metadata
            metadataUpdated = true;
        }

        if (!metadataUpdated && _metadataEqualityComparer != null &&
            !_metadataEqualityComparer.Equals(incomingNodeModel, currentNodeModel))
        {
            yield return new Operation<TModel>(
                OperationType.Update,
                new TModel()
                    .WithId(incomingNodeModel.Id)
                    .WithMetadataFrom(incomingNodeModel));
        }
    }
}
