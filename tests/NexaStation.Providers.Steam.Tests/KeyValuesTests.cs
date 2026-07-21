using NexaStation.Providers.Steam.KeyValues;
using Xunit;

namespace NexaStation.Providers.Steam.Tests;

public sealed class KeyValuesTests
{
    private readonly KeyValuesParser _parser=new();

    [Fact] public void ParsesQuotedKeyValue(){KeyValuesDocument d=_parser.Parse("\"name\" \"Fictional Game\"");Assert.Equal("Fictional Game",d.Root.ValueOf("name"));}
    [Fact] public void ParsesNestedObjects(){KeyValuesDocument d=_parser.Parse("\"root\" { \"child\" { \"value\" \"42\" } }");Assert.Equal("42",d.Root.Find("root")!.Find("child")!.ValueOf("value"));}
    [Fact] public void PreservesRepeatedKeys(){KeyValuesDocument d=_parser.Parse("\"root\" { \"tag\" \"a\" \"tag\" \"b\" }");Assert.Equal(2,d.Root.Find("root")!.FindAll("tag").Count());}
    [Fact] public void HandlesBom(){Assert.Equal("v",_parser.Parse("\uFEFF\"k\" \"v\"").Root.ValueOf("k"));}
    [Fact] public void HandlesEscapedQuotes(){Assert.Equal("say \"hello\"",_parser.Parse("\"k\" \"say \\\"hello\\\"\"").Root.ValueOf("k"));}
    [Fact] public void HandlesEscapedBackslashes(){Assert.Equal("C:\\Games",_parser.Parse("\"path\" \"C:\\\\Games\"").Root.ValueOf("path"));}
    [Fact] public void HandlesEmptyObjects(){KeyValuesDocument d=_parser.Parse("\"empty\" { }");Assert.Empty(d.Root.Find("empty")!.Children);}
    [Fact] public void IgnoresUnknownFields(){KeyValuesDocument d=_parser.Parse("\"future\" \"yes\" \"known\" \"ok\"");Assert.Equal("ok",d.Root.ValueOf("known"));}
    [Fact] public void SupportsCommentsAndTabs(){KeyValuesDocument d=_parser.Parse("// comment\n\"k\"\t\"v\"");Assert.Equal("v",d.Root.ValueOf("k"));}
    [Fact] public void ReportsMalformedInputLocation(){KeyValuesDocument d=_parser.Parse("\"root\" {\n \"key\" \"unterminated");Assert.NotEmpty(d.Diagnostics);Assert.True(d.Diagnostics[0].Line>=2);Assert.True(d.Diagnostics[0].Column>0);}
    [Fact] public void DoesNotRequirePropertyOrder(){KeyValuesDocument d=_parser.Parse("\"z\" \"last\" \"a\" \"first\"");Assert.Equal("first",d.Root.ValueOf("a"));}
}
