﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Scrutor
{
    internal class ServiceTypeSelector : IServiceTypeSelector, ISelector
    {
        public ServiceTypeSelector(IImplementationTypeSelector implementationTypeSelector, IEnumerable<Type> types)
        {
            ImplementationTypeSelector = implementationTypeSelector;
            Types = types;
            Selectors = new List<LifetimeSelector>();
        }

        private IEnumerable<Type> Types { get; }

        private List<LifetimeSelector> Selectors { get; }

        private IImplementationTypeSelector ImplementationTypeSelector { get; }

        public IImplementationTypeSelector FromAssemblyOf<T>()
        {
            return ImplementationTypeSelector.FromAssemblyOf<T>();
        }

        public IImplementationTypeSelector FromAssembliesOf(params Type[] types)
        {
            return ImplementationTypeSelector.FromAssembliesOf(types);
        }

        public IImplementationTypeSelector FromAssembliesOf(IEnumerable<Type> types)
        {
            return ImplementationTypeSelector.FromAssembliesOf(types);
        }

        public IImplementationTypeSelector FromAssemblies(params Assembly[] assemblies)
        {
            return ImplementationTypeSelector.FromAssemblies(assemblies);
        }

        public IImplementationTypeSelector FromAssemblies(IEnumerable<Assembly> assemblies)
        {
            return ImplementationTypeSelector.FromAssemblies(assemblies);
        }

        public void AddFromAttributes()
        {
            ImplementationTypeSelector.AddFromAttributes();
        }

        public IServiceTypeSelector AddClasses()
        {
            return ImplementationTypeSelector.AddClasses();
        }

        public IServiceTypeSelector AddClasses(Action<IImplementationTypeFilter> action)
        {
            return ImplementationTypeSelector.AddClasses(action);
        }

        public ILifetimeSelector AsSelf()
        {
            return As(t => new[] { t });
        }

        public ILifetimeSelector As<T>()
        {
            return As(typeof(T));
        }

        public ILifetimeSelector As(params Type[] types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            return As(types.AsEnumerable());
        }

        public ILifetimeSelector As(IEnumerable<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            return AddSelector(Types.Select(t => Tuple.Create(t, types)));
        }

        public ILifetimeSelector AsImplementedInterfaces()
        {
            return AsTypeInfo(t => t.ImplementedInterfaces);
        }

        public ILifetimeSelector As(Func<Type, IEnumerable<Type>> selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            return AddSelector(Types.Select(t => Tuple.Create(t, selector(t))));
        }

        public void Populate(IServiceCollection services)
        {
            if (Selectors.Count == 0)
            {
                AsSelf();
            }

            foreach (var selector in Selectors)
            {
                selector.Populate(services);
            }
        }

        private ILifetimeSelector AddSelector(IEnumerable<Tuple<Type, IEnumerable<Type>>> types)
        {
            var lifetimeSelector = new LifetimeSelector(this, types);

            Selectors.Add(lifetimeSelector);

            return lifetimeSelector;
        }

        private ILifetimeSelector AsTypeInfo(Func<TypeInfo, IEnumerable<Type>> selector)
        {
            return As(t => selector(t.GetTypeInfo()));
        }

        public ILifetimeSelector AsMatchingInterface()
        {
            return AsMatchingInterface(null);
        }

        public ILifetimeSelector AsMatchingInterface(Action<TypeInfo, IImplementationTypeFilter> action)
        {
            return AsTypeInfo(t => FindMatchingInterface(t, action));
        }

        private static IEnumerable<Type> FindMatchingInterface(TypeInfo t, Action<TypeInfo, IImplementationTypeFilter> action)
        {
            string matchingInterfaceName = "I" + t.Name;
            var matchedInterfaces = GetImplementedInterfacesToMap(t).Where(x => string.Equals(x.Name, matchingInterfaceName, StringComparison.Ordinal));
            Type type;
            if (action != null)
            {
                var filter = new ImplementationTypeFilter(matchedInterfaces);
                action(t, filter);
                type = filter.Types.FirstOrDefault();
            }
            else
            {
                type = matchedInterfaces.FirstOrDefault();
            }
            if (type != null)
            {
                yield return type;
            }
        }

        private static IEnumerable<Type> GetImplementedInterfacesToMap(TypeInfo typeInfo)
        {
            if (!typeInfo.IsGenericType)
            {
                return typeInfo.ImplementedInterfaces;
            }
            if (!typeInfo.IsGenericTypeDefinition)
            {
                return typeInfo.ImplementedInterfaces;
            }
            return FilterMatchingGenericInterfaces(typeInfo);
        }

        private static IEnumerable<Type> FilterMatchingGenericInterfaces(TypeInfo typeInfo)
        {
            var genericTypeParameters = typeInfo.GenericTypeParameters;
            foreach (Type current in typeInfo.ImplementedInterfaces)
            {
                var currentTypeInfo = current.GetTypeInfo();
                if (currentTypeInfo.IsGenericType && currentTypeInfo.ContainsGenericParameters && GenericParametersMatch(genericTypeParameters, currentTypeInfo.GenericTypeArguments))
                {
                    yield return currentTypeInfo.GetGenericTypeDefinition();
                }
            }
        }

        private static bool GenericParametersMatch(Type[] parameters, Type[] interfaceArguments)
        {
            if (parameters.Length != interfaceArguments.Length)
            {
                return false;
            }
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] != interfaceArguments[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}