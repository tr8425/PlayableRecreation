using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace PlayableRecreation
{
    /// <summary>
    /// "나의 통산" — 세이브와 무관하게 플레이어 본인의 전적을 config 폴더의 파일 하나에 쌓는다.
    /// 상대가 언제나 AI 이므로 의미 있는 지표는 식민자 랭킹이 아니라 플레이어 본인의 기록이다.
    ///
    /// 온라인 연동은 없다. 파일 하나가 전부다.
    /// </summary>
    public static class RecordStore
    {
        private const string FileName = "PlayableRecreation_Records.xml";
        private const string RootNode = "playableRecreation";

        private static Dictionary<string, GameRecord> cached;

        public static string FilePath
        {
            get { return Path.Combine(GenFilePaths.ConfigFolderPath, FileName); }
        }

        private static Dictionary<string, GameRecord> All
        {
            get { return cached ?? (cached = Load()); }
        }

        public static GameRecord For(MiniGameDef game)
        {
            if (game == null) return new GameRecord();

            GameRecord record;
            if (!All.TryGetValue(game.defName, out record) || record == null)
            {
                record = new GameRecord();
                All[game.defName] = record;
            }

            return record;
        }

        public static void Record(MiniGameDef game, in MatchResult result)
        {
            For(game).Record(in result);
            Save();
        }

        public static void ResetAll()
        {
            foreach (GameRecord record in All.Values) record.Reset();
            Save();
        }

        public static void Reset(MiniGameDef game)
        {
            For(game).Reset();
            Save();
        }

        private static Dictionary<string, GameRecord> Load()
        {
            Dictionary<string, GameRecord> records = null;

            try
            {
                if (File.Exists(FilePath) && Scribe.mode == LoadSaveMode.Inactive)
                {
                    Scribe.loader.InitLoading(FilePath);
                    try
                    {
                        Scribe_Collections.Look(ref records, "records", LookMode.Value, LookMode.Deep);
                    }
                    finally
                    {
                        Scribe.loader.FinalizeLoading();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Playable Recreation] 통산 기록을 읽지 못했습니다. 새로 시작합니다: " + e.Message);
                Scribe.ForceStop();
                records = null;
            }

            return records ?? new Dictionary<string, GameRecord>();
        }

        public static void Save()
        {
            // 게임 저장/로드 중에는 Scribe 가 이미 쓰이고 있으므로 건드리지 않는다.
            if (Scribe.mode != LoadSaveMode.Inactive) return;

            Dictionary<string, GameRecord> records = All;

            try
            {
                Scribe.saver.InitSaving(FilePath, RootNode);
                try
                {
                    Scribe_Collections.Look(ref records, "records", LookMode.Value, LookMode.Deep);
                }
                finally
                {
                    Scribe.saver.FinalizeSaving();
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Playable Recreation] 통산 기록을 저장하지 못했습니다: " + e.Message);
                Scribe.ForceStop();
            }
        }
    }
}
