using System.Runtime.InteropServices;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.Tests.IO;

public sealed class KnownFoldersTests
{
    // A redirected Downloads folder on an absent drive or share reports a Win32 failure. Every
    // failing HRESULT is the documented file-output error, whatever exception it maps to.
    [Theory]
    [InlineData(unchecked((int)0x80070002), "en-US")] // ERROR_FILE_NOT_FOUND
    [InlineData(unchecked((int)0x80070003), "fr-CA")] // ERROR_PATH_NOT_FOUND
    [InlineData(unchecked((int)0x80070005), "en-US")] // E_ACCESSDENIED
    [InlineData(unchecked((int)0x80070057), "fr-CA")] // E_INVALIDARG
    [InlineData(unchecked((int)0x80004005), "en-US")] // E_FAIL
    public void EveryFailingResultIsAFileOutputError(int result, string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        AdoFileOutputException error = Assert.Throws<AdoFileOutputException>(() => KnownFolders.Downloads(culture,
            (in Guid _, uint _, nint _, out nint path) => { path = 0; return result; }));
        Assert.Equal(Messages.Get(AdoMessage.DownloadsUnavailable, culture), error.Message);
        Assert.NotNull(error.InnerException);
        Assert.Equal(result, error.InnerException.HResult);
    }

    [Fact]
    public void SuccessReturnsThePathAndAnEmptyResultIsAFileOutputError()
    {
        Guid requested = Guid.Empty;
        string path = KnownFolders.Downloads(CultureInfo.InvariantCulture, (in Guid folder, uint _, nint _, out nint buffer) =>
        {
            requested = folder;
            buffer = Marshal.StringToCoTaskMemUni(@"D:\Téléchargements");
            return 0;
        });
        Assert.Equal(@"D:\Téléchargements", path);
        Assert.Equal(new Guid("374DE290-123F-4565-9164-39C4925E467B"), requested);
        Assert.Throws<AdoFileOutputException>(() => KnownFolders.Downloads(CultureInfo.InvariantCulture,
            (in Guid _, uint _, nint _, out nint buffer) => { buffer = 0; return 0; }));
    }
}
