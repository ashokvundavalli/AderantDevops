namespace Aderant.Build.Services {
    public abstract class FlexService {
        protected Context HostContext { get; private set; }

        public string TraceName {
            get { return GetType().Name; }
        }

        public virtual void Initialize(Context hostContext) {
            HostContext = hostContext;
        }
    }

    public interface IFlexService {
        void Initialize(Context context);
    }
}