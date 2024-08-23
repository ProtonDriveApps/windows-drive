using System;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Drive.Services.Shared;

public abstract record IdentifiableDataModel<TId>(TId Id) : IIdentifiable<TId>
    where TId : IEquatable<TId>;
