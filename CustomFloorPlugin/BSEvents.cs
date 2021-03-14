﻿using System;

using UnityEngine;

using Zenject;


namespace CustomFloorPlugin
{
    /// <summary>
    /// Should be pretty self-explanatory, this is a giant wrapper for many events Beat Saber uses
    /// </summary>
    public class BSEvents : IInitializable, IDisposable
    {
        private readonly IBeatmapObjectCallbackController _beatmapObjectCallbackController;
        private readonly BeatmapObjectManager _beatmapObjectManager;
        private readonly GameEnergyCounter _gameEnergyCounter;
        private readonly GameScenesManager _gameScenesManager;
        private readonly ObstacleSaberSparkleEffectManager _obstacleSaberSparkleEffectManager;
        private readonly ScoreController _scoreController;
        private readonly PlayerDataModel _playerDataModel;
        private readonly PrepareLevelCompletionResults _prepareLevelCompletionResults;
        private readonly IDifficultyBeatmap _difficultyBeatmap;
        private float _lastNoteTime;

        public BSEvents(IBeatmapObjectCallbackController beatmapObjectCallbackController,
                        BeatmapObjectManager beatmapObjectManager,
                        GameEnergyCounter gameEnergyCounter,
                        GameScenesManager gameScenesManager,
                        ObstacleSaberSparkleEffectManager obstacleSaberSparkleEffectManager,
                        ScoreController scoreController, PlayerDataModel playerDataModel,
                        PrepareLevelCompletionResults prepareLevelCompletionResults,
                        IDifficultyBeatmap difficultyBeatmap,
                        float lastNoteTime)
        {
            _beatmapObjectCallbackController = beatmapObjectCallbackController;
            _beatmapObjectManager = beatmapObjectManager;
            _gameEnergyCounter = gameEnergyCounter;
            _gameScenesManager = gameScenesManager;
            _obstacleSaberSparkleEffectManager = obstacleSaberSparkleEffectManager;
            _scoreController = scoreController;
            _playerDataModel = playerDataModel;
            _prepareLevelCompletionResults = prepareLevelCompletionResults;
            _difficultyBeatmap = difficultyBeatmap;
            _lastNoteTime = lastNoteTime;
        }

        public event Action<BeatmapEventData> BeatmapEventDidTriggerEvent = delegate { };
        public event Action GameSceneLoadedEvent = delegate { };
        public event Action LevelFinishedEvent = delegate { };
        public event Action LevelFailedEvent = delegate { };
        public event Action NewHighscore = delegate { };
        public event Action<int> NoteWasCutEvent = delegate { };
        public event Action NoteWasMissedEvent = delegate { };
        public event Action ComboDidBreakEvent = delegate { };
        public event Action<int> GoodCutCountDidChangeEvent = delegate { };
        public event Action<int> BadCutCountDidChangeEvent = delegate { };
        public event Action<int> MissCountDidChangeEvent = delegate { };
        public event Action<int, int> AllNotesCountDidChangeEvent = delegate { };
        public event Action MultiplierDidIncreaseEvent = delegate { };
        public event Action<int> ComboDidChangeEvent = delegate { };
        public event Action SabersStartCollideEvent = delegate { };
        public event Action SabersEndCollideEvent = delegate { };
        public event Action<int, int> ScoreDidChangeEvent = delegate { };

        private int allNotesCount = 0;
        private int goodCutCount = 0;
        private int badCutCount = 0;
        private int missCount = 0;
        private int cuttableNotes = 0;
        private int highScore = 0;

        public void Initialize()
        {
            cuttableNotes = _difficultyBeatmap.beatmapData.cuttableNotesType - 1;
            highScore = _playerDataModel.playerData.GetPlayerLevelStatsData(_difficultyBeatmap).highScore;
            _beatmapObjectCallbackController.beatmapEventDidTriggerEvent += BeatmapEventDidTrigger;
            _beatmapObjectManager.noteWasCutEvent += new BeatmapObjectManager.NoteWasCutDelegate(NoteWasCut);
            _beatmapObjectManager.noteWasMissedEvent += NoteWasMissed;
            _gameEnergyCounter.gameEnergyDidReach0Event += LevelFailed;
            _gameScenesManager.transitionDidFinishEvent += GameSceneLoaded;
            _obstacleSaberSparkleEffectManager.sparkleEffectDidStartEvent += SabersStartCollide;
            _obstacleSaberSparkleEffectManager.sparkleEffectDidEndEvent += SabersEndCollide;
            _scoreController.comboDidChangeEvent += ComboDidChange;
            _scoreController.comboBreakingEventHappenedEvent += ComboDidBreak;
            _scoreController.multiplierDidChangeEvent += MultiplierDidChange;
            _scoreController.scoreDidChangeEvent += ScoreDidChange;
        }

        public void Dispose()
        {
            _beatmapObjectCallbackController.beatmapEventDidTriggerEvent -= BeatmapEventDidTrigger;
            _beatmapObjectManager.noteWasCutEvent -= new BeatmapObjectManager.NoteWasCutDelegate(NoteWasCut);
            _beatmapObjectManager.noteWasMissedEvent -= NoteWasMissed;
            _gameEnergyCounter.gameEnergyDidReach0Event -= LevelFailed;
            _gameScenesManager.transitionDidFinishEvent -= GameSceneLoaded;
            _obstacleSaberSparkleEffectManager.sparkleEffectDidStartEvent -= SabersStartCollide;
            _obstacleSaberSparkleEffectManager.sparkleEffectDidEndEvent -= SabersEndCollide;
            _scoreController.comboDidChangeEvent -= ComboDidChange;
            _scoreController.comboBreakingEventHappenedEvent -= ComboDidBreak;
            _scoreController.multiplierDidChangeEvent -= MultiplierDidChange;
            _scoreController.scoreDidChangeEvent -= ScoreDidChange;
        }

        private void BeatmapEventDidTrigger(BeatmapEventData eventData)
        {
            BeatmapEventDidTriggerEvent(eventData);
        }

        private void NoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
            if (noteController.noteData.colorType == ColorType.None || noteController.noteData.beatmapObjectType != BeatmapObjectType.Note)
                return;

            AllNotesCountDidChangeEvent(allNotesCount++, cuttableNotes);
            if (noteCutInfo.allIsOK)
            {
                NoteWasCutEvent((int)noteCutInfo.saberType);
                GoodCutCountDidChangeEvent(goodCutCount++);
            }
            else
            {
                BadCutCountDidChangeEvent(badCutCount++);
            }
            if (Mathf.Approximately(noteController.noteData.time, _lastNoteTime))
            {
                _lastNoteTime = 0f;
                LevelFinishedEvent();
                LevelCompletionResults results = _prepareLevelCompletionResults.FillLevelCompletionResults(LevelCompletionResults.LevelEndStateType.Cleared, LevelCompletionResults.LevelEndAction.None);
                if (results.modifiedScore > highScore)
                    NewHighscore();
            }
        }

        private void NoteWasMissed(NoteController noteController)
        {
            if (noteController.noteData.colorType == ColorType.None || noteController.noteData.beatmapObjectType != BeatmapObjectType.Note)
                return;

            NoteWasMissedEvent();
            AllNotesCountDidChangeEvent(allNotesCount++, cuttableNotes);
            MissCountDidChangeEvent(missCount++);
            if (Mathf.Approximately(noteController.noteData.time, _lastNoteTime))
            {
                _lastNoteTime = 0f;
                LevelFinishedEvent();
                LevelCompletionResults results = _prepareLevelCompletionResults.FillLevelCompletionResults(LevelCompletionResults.LevelEndStateType.Cleared, LevelCompletionResults.LevelEndAction.None);
                if (results.modifiedScore > highScore)
                    NewHighscore();
            }
        }

        private void LevelFailed()
        {
            LevelFailedEvent();
        }

        private void GameSceneLoaded(ScenesTransitionSetupDataSO setupData, DiContainer container)
        {
            GameSceneLoadedEvent();
        }

        private void SabersStartCollide(SaberType saberType)
        {
            SabersStartCollideEvent();
        }

        private void SabersEndCollide(SaberType saberType)
        {
            SabersEndCollideEvent();
        }

        private void ComboDidChange(int combo)
        {
            ComboDidChangeEvent(combo);
        }

        private void ComboDidBreak()
        {
            ComboDidBreakEvent();
        }

        private void MultiplierDidChange(int multiplier, float progress)
        {
            if (multiplier > 1 && progress < 0.1f)
            {
                MultiplierDidIncreaseEvent();
            }
        }

        private void ScoreDidChange(int rawScore, int modifiedScore)
        {
            ScoreDidChangeEvent(rawScore, modifiedScore);
        }
    }
}
