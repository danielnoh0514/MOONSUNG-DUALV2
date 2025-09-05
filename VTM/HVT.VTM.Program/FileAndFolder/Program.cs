using HVT.VTM.Base;
using HVT.Utility;
using PropertyChanged;

namespace HVT.VTM.Program
{
    [AddINotifyPropertyChangedInterface]

    public partial class Program
    {
        public FolderMap FolderMap = new FolderMap();
        public void CreatAppFolder()
        {
            FolderMap.TryCreatFolderMap();
        }

        public AppSetting AppSetting { get; set; } = new AppSetting();

        public void LoadAppSetting()
        {
            AppSetting = Extensions.OpenFromFile<AppSetting>("Config.cfg");
            AppSetting = AppSetting ?? new AppSetting();
        }
    }
}
