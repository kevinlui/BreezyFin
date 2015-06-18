using System.Collections.ObjectModel;

using Breezy.Fin.ViewModel;


namespace Breezy.Fin.Model
{
    public interface IBroker
    {
        void Connect();

        ObservableCollection<Asset> GetAssets();
    }
}
