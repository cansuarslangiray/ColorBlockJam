using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardBlockConditionService
    {
        private readonly Dictionary<int, RuntimeConditionState> _conditionByBlockId = new();

        public int TotalMoveCount { get; private set; }

        public void Setup(IReadOnlyDictionary<int, RuntimeBlockState> runtimeBlocks)
        {
            TotalMoveCount = 0;
            _conditionByBlockId.Clear();

            if (runtimeBlocks == null)
            {
                return;
            }

            foreach (var pair in runtimeBlocks)
            {
                var block = pair.Value;
                var features = block.BlockFeatures;
                var hasMaxMovesFeature = features.HasFeature(BlockFeature.MaxMovesBeforeExit) &&
                                         block.MaxMovesBeforeExit > 0;
                var hasMinClearedFeature = features.HasFeature(BlockFeature.MinClearedBlocksBeforeExit) &&
                                           block.MinClearedBlocksBeforeExit > 0;
                if (!hasMaxMovesFeature && !hasMinClearedFeature)
                {
                    continue;
                }

                var maxMovesLimit = hasMaxMovesFeature ? Mathf.Max(1, block.MaxMovesBeforeExit) : 0;
                var minClearedRequirement = hasMinClearedFeature ? Mathf.Max(1, block.MinClearedBlocksBeforeExit) : 0;
                _conditionByBlockId[pair.Key] = new RuntimeConditionState
                {
                    HasMaxMovesBeforeExit = hasMaxMovesFeature,
                    MaxMovesBeforeExitLimit = maxMovesLimit,
                    SpawnMoveCount = TotalMoveCount,
                    RemainingMovesBeforeExit = maxMovesLimit,
                    HasMinClearedBlocksBeforeExit = hasMinClearedFeature,
                    RemainingClearedBlocksBeforeExit = minClearedRequirement
                };
            }
        }

        public bool IsBlockLocked(int blockId)
        {
            return _conditionByBlockId.TryGetValue(blockId, out var state) && state.IsLocked;
        }

        public void ConsumeSuccessfulMove(int movedBlockId, bool movedBlockCleared, out bool levelFailed)
        {
            levelFailed = false;

            if (movedBlockCleared)
            {
                _conditionByBlockId.Remove(movedBlockId);
            }

            if (movedBlockCleared && _conditionByBlockId.Count > 0)
            {
                foreach (var state in _conditionByBlockId.Values)
                {
                    if (!state.HasMinClearedBlocksBeforeExit ||
                        state.RemainingClearedBlocksBeforeExit <= 0)
                    {
                        continue;
                    }

                    state.RemainingClearedBlocksBeforeExit =
                        Mathf.Max(0, state.RemainingClearedBlocksBeforeExit - 1);
                    if (state.RemainingClearedBlocksBeforeExit == 0)
                    {
                        state.HasMinClearedBlocksBeforeExit = false;
                    }
                }
            }

            TotalMoveCount++;
            if (_conditionByBlockId.Count == 0)
            {
                return;
            }

            foreach (var state in _conditionByBlockId.Values)
            {
                if (!state.HasMaxMovesBeforeExit)
                {
                    continue;
                }

                var nextRemaining = state.MaxMovesBeforeExitLimit - (TotalMoveCount - state.SpawnMoveCount);
                state.RemainingMovesBeforeExit = nextRemaining;

                if (state.RemainingMovesBeforeExit <= 0)
                {
                    levelFailed = true;
                }
            }
        }

        public bool TryGetIndicatorState(int blockId, out BlockConditionIndicatorState indicatorState)
        {
            if (!_conditionByBlockId.TryGetValue(blockId, out var state))
            {
                indicatorState = default;
                return false;
            }

            var showMaxMoves = state.HasMaxMovesBeforeExit;
            var showMinCleared = state.HasMinClearedBlocksBeforeExit && state.RemainingClearedBlocksBeforeExit > 0;
            if (!showMaxMoves && !showMinCleared)
            {
                indicatorState = default;
                return false;
            }

            var text = showMinCleared
                ? Mathf.Max(0, state.RemainingClearedBlocksBeforeExit).ToString()
                : Mathf.Max(0, state.RemainingMovesBeforeExit).ToString();

            indicatorState = new BlockConditionIndicatorState(true, text);
            return true;
        }

        private sealed class RuntimeConditionState
        {
            public bool HasMaxMovesBeforeExit;
            public int MaxMovesBeforeExitLimit;
            public int SpawnMoveCount;
            public int RemainingMovesBeforeExit;
            public bool HasMinClearedBlocksBeforeExit;
            public int RemainingClearedBlocksBeforeExit;

            public bool IsLocked => HasMinClearedBlocksBeforeExit && RemainingClearedBlocksBeforeExit > 0;
        }
    }
}
