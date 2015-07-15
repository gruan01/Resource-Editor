using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ResEditor {
    public class Converter1 : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            var v = (string)value;
            var tmp = v.Split('_');
            //if (tmp.Length > 2)
            //    return string.Format("{0}_{1}", tmp[0], tmp[1]);
            //else 
            if (tmp.Length > 1)
                return tmp[0];
            else
                return "未分组";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
