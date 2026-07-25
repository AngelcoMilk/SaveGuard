using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using YAPYAP;

namespace SaveGuard;

internal static class SaveBackupService
{
    private static readonly MethodInfo SaveGameDataMethod = AccessTools.Method(typeof(GameManager), "SaveGameData");

    internal static void TryCreateQuotaFailureBackup(GameManager gameManager)
    {
        if (Plugin.CreateEmergencyBackup?.Value != true || gameManager == null || !Service.Get<SaveManager>(out SaveManager saveManager))
        {
            return;
        }

        try
        {
            if (SaveGameDataMethod == null)
            {
                throw new MissingMethodException(typeof(GameManager).FullName, "SaveGameData");
            }

            // The final quota round does not write a save before entering the Astral Plane.
            // Persist the live pre-reset state first so the emergency copy is not one night stale.
            SaveGameDataMethod.Invoke(gameManager, null);

            int slot = saveManager.CurrentSlot;
            string saveDirectory = Path.Combine(Application.persistentDataPath, "saves");
            string source = Path.Combine(saveDirectory, $"save_slot_{slot}.json");
            if (!File.Exists(source))
            {
                Plugin.Debug($"No on-disk save found for slot {slot}; emergency backup skipped.");
                return;
            }

            string backupDirectory = Path.Combine(saveDirectory, "SaveGuardBackups");
            Directory.CreateDirectory(backupDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string destination = Path.Combine(backupDirectory, $"save_slot_{slot}_{stamp}.json");
            File.Copy(source, destination, overwrite: false);
            PruneBackups(backupDirectory, Plugin.MaxEmergencyBackups.Value);
            Plugin.Log?.LogInfo($"Created quota-failure emergency backup: {destination}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Unable to create quota-failure emergency backup: " + ex.Message);
        }
    }

    private static void PruneBackups(string directory, int maximum)
    {
        FileInfo[] files = new DirectoryInfo(directory)
            .GetFiles("save_slot_*.json")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        for (int i = maximum; i < files.Length; i++)
        {
            try
            {
                files[i].Delete();
            }
            catch (Exception ex)
            {
                Plugin.Debug("Unable to prune old backup " + files[i].FullName + ": " + ex.Message);
            }
        }
    }
}
