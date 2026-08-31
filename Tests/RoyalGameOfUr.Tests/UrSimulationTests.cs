using System;
using RoyalGameOfUr.Core;
using Xunit;

namespace RoyalGameOfUr.Tests
{
    /// <summary>
    /// M1 완료 기준: 랜덤 대 랜덤 10,000판에서 예외 없이, 무한루프 없이, 반드시 승자가 나온다.
    /// </summary>
    public class UrSimulationTests
    {
        private const int Games = 10000;
        private const int PlyLimit = 3000;

        [Fact]
        public void 랜덤_대_랜덤_10000판이_모두_정상_종료된다()
        {
            var rng = new Random(20260830);
            var buffer = new UrMove[UrRules.MaxMoves];

            int playerWins = 0, botWins = 0;
            long totalPlies = 0;
            int maxPlies = 0;

            for (int game = 0; game < Games; game++)
            {
                var state = UrGameState.NewGame(game % 2 == 0 ? Side.Player : Side.Bot);
                int seed = rng.Next();
                int diceIndex = 0;
                int plies = 0;

                while (!UrRules.IsGameOver(in state))
                {
                    Assert.True(++plies <= PlyLimit,
                        $"게임 {game}: {PlyLimit}수를 넘겨 종료되지 않음. 상태={state}");
                    Assert.True(state.IsConsistent(), $"게임 {game} {plies}수: 불변식 위반. 상태={state}");

                    int roll = UrDice.Roll(seed, diceIndex++).Total;
                    int count = UrRules.GenerateMoves(in state, roll, buffer);

                    if (count == 0)
                    {
                        state = UrRules.PassTurn(in state);
                        continue;
                    }

                    var move = buffer[rng.Next(count)];
                    var before = state;
                    state = UrRules.Apply(in state, move, out bool extraTurn);

                    // 추가 턴이 아니면 반드시 턴이 넘어가야 한다.
                    if (!extraTurn) Assert.NotEqual(before.Turn, state.Turn);
                    else Assert.Equal(before.Turn, state.Turn);
                }

                Assert.True(state.IsConsistent());
                Side winner = UrRules.Winner(in state).Value;
                Assert.Equal(UrBoardLayout.PieceCount, state.Scored(winner));

                if (winner == Side.Player) playerWins++; else botWins++;
                totalPlies += plies;
                if (plies > maxPlies) maxPlies = plies;
            }

            Assert.Equal(Games, playerWins + botWins);

            // 완전 랜덤끼리면 선공 이점을 감안해도 승률이 한쪽으로 크게 쏠리지 않는다.
            double playerRate = playerWins / (double)Games;
            Assert.InRange(playerRate, 0.40, 0.60);

            // 한 판의 길이가 상식적인 범위인지 (경험적으로 평균 100수 내외)
            double avgPlies = totalPlies / (double)Games;
            Assert.InRange(avgPlies, 20, 400);
            Assert.True(maxPlies < PlyLimit);
        }

        [Fact]
        public void 같은_시드는_같은_대국을_재현한다()
        {
            string Replay()
            {
                var sb = new System.Text.StringBuilder();
                var state = UrGameState.NewGame(Side.Player);
                var buffer = new UrMove[UrRules.MaxMoves];
                int diceIndex = 0;

                while (!UrRules.IsGameOver(in state) && diceIndex < PlyLimit)
                {
                    int roll = UrDice.Roll(4242, diceIndex++).Total;
                    int count = UrRules.GenerateMoves(in state, roll, buffer);
                    if (count == 0) { state = UrRules.PassTurn(in state); sb.Append("P;"); continue; }

                    // 결정론적 선택: 항상 첫 번째 합법수
                    var move = buffer[0];
                    sb.Append(move).Append(';');
                    state = UrRules.Apply(in state, move, out _);
                }

                return sb.ToString();
            }

            Assert.Equal(Replay(), Replay());
        }

        [Fact]
        public void 무르기는_주사위를_재추첨하지_않는다()
        {
            // 순번을 되감으면 반드시 같은 눈이 나온다 (DESIGN.md P4).
            const int seed = 999;
            int[] forward = new int[20];
            for (int i = 0; i < 20; i++) forward[i] = UrDice.Roll(seed, i).Total;

            for (int undoTo = 19; undoTo >= 0; undoTo--)
                Assert.Equal(forward[undoTo], UrDice.Roll(seed, undoTo).Total);
        }
    }
}
