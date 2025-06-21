using System.Collections.ObjectModel;
using System.Globalization;
using vtrace.ViewModels;

namespace vtrace.Convecters;


public class AnyItemConnectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObservableCollection<VlessConfigViewModel> collection)
        {
            return collection.Any(c => c.IsConnected);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}