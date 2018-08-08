using System;

namespace Aderant.Build.Services {
    internal interface IContextualServiceProvider : IServiceProvider {
        T GetService<T>(BuildOperationContext context, string contractName = null, string scope = null);

        //void Initialize(Context context);
    }
}