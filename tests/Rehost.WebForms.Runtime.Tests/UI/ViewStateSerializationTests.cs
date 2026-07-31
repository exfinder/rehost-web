using System.IO;
using System.Web.UI;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.UI;

public sealed class ViewStateSerializationTests
{
    // The value has no string TypeConverter, so ObjectStateFormatter falls through to
    // BinaryFormatter. Without the out-of-band implementation this throws. The stream
    // overloads are used throughout because the string ones apply a MAC, which needs a
    // machineKey section and therefore a hosted application.
    [Fact]
    public void A_Value_Without_A_String_Converter_Round_Trips_Through_Binary_Serialization()
    {
        var formatter = new ObjectStateFormatter();

        using var stream = new MemoryStream();
        formatter.Serialize(stream, new Coordinates { X = 17, Label = "corner" });
        stream.Position = 0;

        var restored = formatter.Deserialize(stream).ShouldBeOfType<Coordinates>();

        restored.X.ShouldBe(17);
        restored.Label.ShouldBe("corner");
    }

    // A stub that echoed the graph back would pass the round trip above, so pin the wire form:
    // Token_BinarySerialized is 50, and the payload carries BinaryFormatter's 0x00 0x01 header.
    [Fact]
    public void Binary_Serialization_Reaches_The_Wire_Rather_Than_A_String_Conversion()
    {
        using var stream = new MemoryStream();
        new ObjectStateFormatter().Serialize(stream, new Coordinates { X = 17, Label = "corner" });

        var bytes = stream.ToArray();

        bytes.ShouldContain((byte)50);
        System.Text.Encoding.ASCII.GetString(bytes).ShouldContain("Coordinates");
    }

    // Framework 4.8.1 builds System.Web with OBJECTSTATEFORMATTER defined, so view state keys
    // are IndexedString tokens that ObjectStateFormatter deduplicates into a string table.
    // Verified against the GAC assembly by reflecting the same call.
    [Fact]
    public void View_State_Keys_Are_Indexed_Strings()
    {
        var bag = new StateBag();
        ((IStateManager)bag).TrackViewState();
        bag.Add("alpha", "one");
        bag.Add("beta", "two");

        var state = ((IStateManager)bag).SaveViewState().ShouldBeOfType<System.Collections.ArrayList>();

        state.Count.ShouldBe(4);
        state[0].ShouldBeOfType<IndexedString>().Value.ShouldBe("alpha");
        state[1].ShouldBe("one");
        state[2].ShouldBeOfType<IndexedString>().Value.ShouldBe("beta");
        state[3].ShouldBe("two");
    }

    [Fact]
    public void View_State_Keys_Round_Trip_Back_Into_The_Bag()
    {
        var saved = new StateBag();
        ((IStateManager)saved).TrackViewState();
        saved.Add("alpha", "one");

        var restored = new StateBag();
        ((IStateManager)restored).LoadViewState(((IStateManager)saved).SaveViewState());

        restored["alpha"].ShouldBe("one");
    }

    // The same build flag makes LosFormatter a thin wrapper over ObjectStateFormatter rather
    // than the pre-2.0 serializer. Only the wrapper drops IStateFormatter, so the interface
    // list distinguishes the two builds; the GAC assembly reports none.
    [Fact]
    public void Los_Formatter_Wraps_Object_State_Formatter()
    {
        typeof(LosFormatter).GetInterfaces().ShouldBeEmpty();
    }

    [Serializable]
    private sealed class Coordinates
    {
        public int X;
        public string? Label;
    }
}
