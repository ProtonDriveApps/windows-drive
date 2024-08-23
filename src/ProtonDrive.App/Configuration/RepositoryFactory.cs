using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.App.Configuration;

internal class RepositoryFactory : IRepositoryFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AppConfig _appConfig;

    public RepositoryFactory(ILoggerFactory loggerFactory, AppConfig appConfig)
    {
        _loggerFactory = loggerFactory;
        _appConfig = appConfig;
    }

    public IRepository<T> GetRepository<T>()
    {
        var fileName = Path.Combine(_appConfig.AppDataPath, $"{NameOf(typeof(T))}.json");

        return GetCachingRepository<T>(fileName);
    }

    public ICollectionRepository<T> GetCachingCollectionRepository<T>(string fileName)
    {
        return new CollectionRepository<T>(GetCachingRepository<IEnumerable<T>>(fileName));
    }

    public ICollectionRepository<T> GetCollectionRepository<T>(string fileName)
    {
        return new CollectionRepository<T>(GetRepository<IEnumerable<T>>(fileName));
    }

    public IRepository<T> GetCachingRepository<T>(string fileName)
    {
        return new CachingRepository<T>(GetRepository<T>(fileName));
    }

    public IRepository<T> GetRepository<T>(string fileName)
    {
        return
            new SafeRepository<T>(
                new LoggingRepository<T>(
                    _loggerFactory.CreateLogger<LoggingRepository<T>>(),
                    new FileRepository<T>(
                        new JsonUtf8Serializer(),
                        GetFullFileName(fileName))));
    }

    private string GetFullFileName(string fileName)
    {
        // If fileName contains a rooted path, the AppConfig.AppDataPath is ignored
        return Path.Combine(_appConfig.AppDataPath, fileName);
    }

    private static string NameOf(Type type)
    {
        if (IsEnumerableType(type) && type.GetGenericArguments().Any())
        {
            return $"{type.GetGenericArguments()[0].Name}s";
        }

        return type.Name;
    }

    private static bool IsEnumerableType(Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }
}
