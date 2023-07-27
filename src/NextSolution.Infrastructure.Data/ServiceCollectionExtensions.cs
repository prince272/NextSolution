﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NextSolution.Core.Utilities;
using NextSolution.Core.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NextSolution.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationRepositories(this IServiceCollection services)
        {
            var repositoryTypes = TypeHelper.GetTypesFromApplicationDependencies().Where(type => type.IsClass && !type.IsAbstract && type.IsCompatibleWith(typeof(IRepository<>)));

            foreach (var concreteType in repositoryTypes)
            {
                var matchingInterfaceType = concreteType.GetInterfaces().FirstOrDefault(x => string.Equals(x.Name, $"I{concreteType.Name}", StringComparison.Ordinal));
               
                if (matchingInterfaceType != null)
                {
                    services.AddScoped(matchingInterfaceType, concreteType);
                }
            }

            return services;
        }
    }
}
