namespace Aderant.Build.Services {

    public abstract class FlexService : IFlexService {
        public virtual void Initialize(Context hostContext) {
        }
    }

    public interface IFlexService {
        void Initialize(Context context);
    }

}