using Caliburn.Micro;

namespace ResEditor.ViewModels {
    public class ShellViewModel : Screen, IShell {

        public Screen VM {
            get;
            set;
        }

        public ShellViewModel() {
            this.VM = new EditorViewModel();
        }

    }
}