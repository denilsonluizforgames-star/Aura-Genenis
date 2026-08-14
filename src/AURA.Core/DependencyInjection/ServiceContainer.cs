using System;
using System.Collections.Generic;

namespace AURA.Core.DependencyInjection
{
    /// <summary>
    /// A minimal dependency injection container. AURA deliberately avoids a
    /// third-party DI framework in the Core MVP to keep the project buildable
    /// with zero NuGet restore requirements.
    /// </summary>
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<object>> _factories = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// Registers a pre-built singleton instance for the given service type.
        /// </summary>
        public void RegisterInstance<TService>(TService instance)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            _instances[typeof(TService)] = instance;
        }

        /// <summary>
        /// Registers a factory used to lazily create a singleton the first
        /// time it is resolved.
        /// </summary>
        public void RegisterFactory<TService>(Func<TService> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            _factories[typeof(TService)] = () => factory();
        }

        public TService Resolve<TService>()
        {
            Type type = typeof(TService);

            object existing;
            if (_instances.TryGetValue(type, out existing))
            {
                return (TService)existing;
            }

            Func<object> factory;
            if (_factories.TryGetValue(type, out factory))
            {
                object created = factory();
                _instances[type] = created;
                return (TService)created;
            }

            throw new InvalidOperationException(
                "No registration found for service type " + type.FullName + ".");
        }

        public bool IsRegistered<TService>()
        {
            Type type = typeof(TService);
            return _instances.ContainsKey(type) || _factories.ContainsKey(type);
        }
    }
}
