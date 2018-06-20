using System;

namespace Aderant.Build.Services {
    internal interface IContextualServiceProvider : IServiceProvider {
        T GetService<T>(Context context, string contractName = null, string scope = null);

        //void Initialize(Context context);
    }
}