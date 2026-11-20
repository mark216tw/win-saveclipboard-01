using System.Text;
using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SaveClipboard.Tests;

public class HistoryPersistenceServiceTests
{
    [Fact]
    public void SaveAndLoad_以DPAPI加密並還原文字與圖片()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SaveClipboardTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.dat");
        try
        {
            var service = new HistoryPersistenceService(path, NullLogger<HistoryPersistenceService>.Instance);
            var history = new ClipboardHistoryService(10);
            history.AddText("secret-text");
            history.AddImage([1, 2, 3, 4], [9, 8]);

            Assert.True(service.TrySave(history.Items.ToList()));

            byte[] encrypted = File.ReadAllBytes(path);
            string asText = Encoding.UTF8.GetString(encrypted);
            Assert.DoesNotContain("secret-text", asText);

            IReadOnlyList<ClipboardEntry> loaded = service.Load();
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, entry => entry.Kind == ClipboardEntryKind.Text && entry.Text == "secret-text");
            Assert.Contains(loaded, entry =>
                entry.Kind == ClipboardEntryKind.Image
                && entry.ImageBytes is not null
                && entry.ImageBytes.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Delete_移除加密歷史檔()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SaveClipboardTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.dat");
        try
        {
            var service = new HistoryPersistenceService(path, NullLogger<HistoryPersistenceService>.Instance);
            var history = new ClipboardHistoryService(10);
            history.AddText("item");
            Assert.True(service.TrySave(history.Items.ToList()));

            service.Delete();

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_相容舊版Base64字串格式的加密檔案()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SaveClipboardTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.dat");
        try
        {
            Directory.CreateDirectory(directory);
            string legacyJson = """
                {
                  "Version": 1,
                  "Entries": [
                    {
                      "Id": "11111111-1111-1111-1111-111111111111",
                      "Kind": "Image",
                      "ImageBase64": "AQIDBA==",
                      "ThumbnailBase64": "CQg=",
                      "PixelHashBase64": "",
                      "Key": "I:TEST",
                      "CopiedAt": "2026-11-16T00:00:00+00:00",
                      "IsPinned": true
                    }
                  ]
                }
                """;
            byte[] encrypted = System.Security.Cryptography.ProtectedData.Protect(
                Encoding.UTF8.GetBytes(legacyJson),
                optionalEntropy: null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);

            var service = new HistoryPersistenceService(path, NullLogger<HistoryPersistenceService>.Instance);
            IReadOnlyList<ClipboardEntry> loaded = service.Load();

            ClipboardEntry entry = Assert.Single(loaded);
            Assert.Equal(ClipboardEntryKind.Image, entry.Kind);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, entry.ImageBytes);
            Assert.Equal(new byte[] { 9, 8 }, entry.ThumbnailBytes);
            Assert.Null(entry.ImagePixelHash);
            Assert.True(entry.IsPinned);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
