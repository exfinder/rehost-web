using System.Collections;
using System.Globalization;
using System.IO;
using System.Web.UI;

// ObjectStateFormatter implements IFormatter, which lives in the out-of-band
// System.Runtime.Serialization.Formatters rather than the shared framework's lower-versioned copy.
// A page reference set built from the shared framework directory cannot bind it.
public partial class FormatterPage : Page
{
    protected string SerializedLength
    {
        get
        {
            using (var stream = new MemoryStream())
            {
                new ObjectStateFormatter().Serialize(stream, new ArrayList { "alpha", 42 });

                return stream.Length.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
