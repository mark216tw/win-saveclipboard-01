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
}
