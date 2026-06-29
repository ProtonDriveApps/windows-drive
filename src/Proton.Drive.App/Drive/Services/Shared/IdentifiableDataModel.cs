using Proton.Drive.Shared;

namespace Proton.Drive.App.Drive.Services.Shared;

public abstract record IdentifiableDataModel<TId>(TId Id) : IIdentifiable<TId>
    where TId : IEquatable<TId>;
