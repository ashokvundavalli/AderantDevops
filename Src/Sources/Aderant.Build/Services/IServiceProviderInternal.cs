using System;

namespace Aderant.Build.Services {
    internal interface IServiceProviderInternal : IServiceProvider {
        T GetService<T>(string contractName = null, string contextValue = null);

        void Initialize(Context context);
    }
}