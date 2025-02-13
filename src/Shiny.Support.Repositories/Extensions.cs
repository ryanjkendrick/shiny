﻿
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.Support.Repositories;

namespace Shiny;


public static class RepositoryExtensions
{
    #if PLATFORM
    public static IServiceCollection AddDefaultRepository(this IServiceCollection services)
    {
        services.TryAddSingleton<IRepository, Shiny.Support.Repositories.Impl.FileSystemRepository>();
        return services;
    }

    #endif

    public static bool Remove<T>(this IRepository repository, T item) where T : IRepositoryEntity
        => repository.Remove<T>(item.Identifier);
}
