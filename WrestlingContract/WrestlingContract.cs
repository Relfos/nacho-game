using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Numerics;
using System.Runtime.CompilerServices;

/*
 * TODO:
 * + add player item inventory
 * + add equip item to wrestler
 * + add special moves
 * + add AI bot
 */

namespace WrestlingContract
{
    public class WrestlingContract : SmartContract
    {
        public static readonly byte[] Developers_Address = "AbZJjZ5F1x82VybfsqM7zi4nkWoX8uwepy".ToScriptHash();

        private static readonly byte[] gas_asset_id = { 96, 44, 121, 113, 139, 22, 228, 66, 222, 88, 119, 142, 20, 141, 11, 16, 132, 227, 178, 223, 253, 93, 230, 183, 177, 108, 238, 121, 105, 40, 45, 231 };

        private const ulong gas_decimals = 100000000;
        private const ulong gas_registration_price = 0 * gas_decimals; // free for now

        public static bool Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                // param Owner must be script hash
                bool isOwner = Runtime.CheckWitness(Developers_Address);

                if (isOwner)
                {
                    return true;
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                /*if (operation == "symbol") return Symbol();

                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }

*/
            }

            // TODO allow withdrawls
            return false;
        }

        #region UTILS 

        // get smart contract script hash
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // checks how many gas was sent with the transaction
        private static BigInteger GetContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            BigInteger value = 0;
            var receiver = GetReceiver();

            // get the total amount of gas
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == receiver && output.AssetId == gas_asset_id)
                {
                    value += output.Value;
                }
            }
            return value;
        }
        #endregion

        #region MATH

        private static readonly ulong seconds_per_day = 86400;
        private static readonly ulong seconds_per_hour = 3600;

        private static readonly ulong RND_A = 16807;
        private static readonly ulong RND_M = 2147483647;

        // returns a first initial random number
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BigInteger Randomize(byte[] init)
        {
            var height = Blockchain.GetHeight();
            Header header = Blockchain.GetHeader(height);
            BigInteger seed = header.ConsensusData;
            byte[] temp = seed.AsByteArray();

            // reseed with address hash
            temp = temp.Concat(init);
            seed = temp.AsBigInteger();
            return seed;
        }

        // returns a next random number (seed must be initialized with Randomize)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BigInteger NextRandom(BigInteger seed)
        {
            return ((RND_A * seed) % RND_M);
        }

        // calculates integer approximation of Sqrt(n)
        private static BigInteger Sqrt(BigInteger n)
        {
            BigInteger root = n / 2;

            while (n < root * root)
            {
                root += n / root;
                root /= 2;
             }

            return root;
        }

        #endregion

        #region ACCOUNT API

        private static readonly byte[] account_balance_prefix = { (byte)'A', (byte)'.', (byte)'B' };
        private static readonly byte[] account_wrestler_total_prefix = { (byte)'A', (byte)'.', (byte)'T' };
        private static readonly byte[] account_wrestler_list_prefix = { (byte)'A', (byte)'.', (byte)'L' };

        // NOTE - this should require GAS later!!!
        public static bool RegisterAccount(byte[] addressHash)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return false;
            }

            if (!UseAccountBalance(addressHash, gas_registration_price))
            {
                return false;
            }

            byte[] key;

            // store how many wrestlers
            key = account_wrestler_total_prefix.Concat(addressHash);
            BigInteger total = 1;
            Storage.Put(Storage.CurrentContext, key, total);

            // generate one wrestler and store it
            var wrestler_id = GenerateWrestler(addressHash);
            BigInteger index = 0;
            key = account_wrestler_list_prefix.Concat(index.AsByteArray());
            key = key.Concat(addressHash);
            Storage.Put(Storage.CurrentContext, key, wrestler_id);

            return true;
        }

        public static bool TopUpAccountBalance(byte[] addressHash)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return false;
            }

            var new_amount = GetContributeValue();
            if (new_amount <= 0)
            {
                return false;
            }

            UpdateAccountBalance(addressHash);

            return true;
        }

        // cost will be subtracted from balance, returns true if sucessful
        private static bool UseAccountBalance(byte[] addressHash, BigInteger cost)
        {
            if (cost < 0)
            {
                return false;
            }

            var new_amount = GetContributeValue();
            var key = account_balance_prefix.Concat(addressHash);
            var old_balance = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            var new_balance = old_balance + new_amount;
            new_balance -= cost;

            if (new_balance < 0)
            {
                return false;
            }

            if (new_balance != old_balance)
            {
                Storage.Put(Storage.CurrentContext, key, new_balance);
            }

            return true;
        }

        // returns true if account has balance greater than zero
        private static bool UpdateAccountBalance(byte[] addressHash)
        {
            var new_amount = GetContributeValue();
            var key = account_balance_prefix.Concat(addressHash);
            var old_balance = Storage.Get(Storage.CurrentContext, key).AsBigInteger();

            var new_balance = old_balance + new_amount;

            if (new_balance != old_balance)
            {
                Storage.Put(Storage.CurrentContext, key, new_balance);
            }

            return new_balance > 0;
        }

        // returns how much GAS an account owns in the contract
        public static BigInteger GetAccountBalance(byte[] addressHash)
        {
            var key = account_balance_prefix.Concat(addressHash);
            var balance = Storage.Get(Storage.CurrentContext, key).AsBigInteger();
            return balance;
        }

        // get how many wrestlers in an account
        public static BigInteger GetAccountWrestlerCount(byte[] addressHash)
        {
            var key = account_wrestler_total_prefix.Concat(addressHash);
            var temp = Storage.Get(Storage.CurrentContext, key);
            return temp.AsBigInteger();
        }

        // returns wrestlerID at specified index slot of account
        public static BigInteger GetAccountWrestlerByIndex(byte[] addressHash, BigInteger index)
        {
            var key = account_wrestler_list_prefix.Concat(index.AsByteArray());
            key = key.Concat(addressHash);
            var temp = Storage.Get(Storage.CurrentContext, key);
            return temp.AsBigInteger();
        }
        #endregion

        #region WRESTLER API
        private static readonly byte[] wrestler_last_id_key = { (byte)'W', (byte)'I', (byte)'D' };
        private static readonly byte[] wrestler_owner_prefix = { (byte)'W', (byte)'.', (byte)'O' };
        private static readonly byte[] wrestler_stats_prefix = { (byte)'W', (byte)'.', (byte)'S' };
        private static readonly byte[] wrestler_score_prefix = { (byte)'W', (byte)'.', (byte)'Z' };
        private static readonly byte[] wrestler_location_prefix = { (byte)'W', (byte)'.', (byte)'L' };
        private static readonly byte[] wrestler_visual_prefix = { (byte)'W', (byte)'.', (byte)'V' };
        private static readonly byte[] wrestler_experience_prefix = { (byte)'W', (byte)'.', (byte)'X' };
        private static readonly byte[] wrestler_timestamp_prefix = { (byte)'W', (byte)'.', (byte)'T' };
        private static readonly byte[] wrestler_battle_prefix = { (byte)'W', (byte)'.', (byte)'B' };

        private static readonly byte WRESTLER_LOCATION_NONE = 0;
        private static readonly byte WRESTLER_LOCATION_QUEUE = 1;
        private static readonly byte WRESTLER_LOCATION_BATTLE = 2;
        private static readonly byte WRESTLER_LOCATION_AUCTION = 3;
        private static readonly byte WRESTLER_LOCATION_GYM = 4;

        private static readonly ulong WRESTLER_MAX_XP = 1488400;
        

        // cannot be called directly!
        private static BigInteger GenerateWrestler(byte[] addressHash)
        {
            byte[] key;

            var wrestler_id_key = Storage.Get(Storage.CurrentContext, wrestler_last_id_key);
            BigInteger wrestler_id = wrestler_id_key.AsBigInteger();
            wrestler_id = wrestler_id + 1;
            // update last wrestler ID
            Storage.Put(Storage.CurrentContext, wrestler_last_id_key, wrestler_id);

            // save owner of this wrestler
            key = wrestler_owner_prefix.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, key, addressHash);

            // note - wrestling State, Level, Experience and Score is not initilize here to save GAS, since those wil be zero at start 

            // initialize random generator
            BigInteger seed = Randomize(addressHash);

            // save visual of this wrestler
            key = wrestler_visual_prefix.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, key, seed);

            // get next random number
            seed = NextRandom(seed);

            // save initial stats of this wrestler
            key = wrestler_stats_prefix.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, key, seed);

            return wrestler_id;
        }

        // returns script hash who owns wrestler
        public static byte[] GetWrestlerOwner(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_owner_prefix.Concat(temp);
            return Storage.Get(Storage.CurrentContext, key);
        }

        // returns seed used for getting visual
        public static BigInteger GetWrestlerVisual(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_visual_prefix.Concat(temp);
            var temp2 = Storage.Get(Storage.CurrentContext, key);
            return temp2.AsBigInteger();
        }

        // returns seed used for getting base stats
        public static BigInteger GetWrestlerStats(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_stats_prefix.Concat(temp);
            var temp2 = Storage.Get(Storage.CurrentContext, key);
            return temp2.AsBigInteger();
        }

        // returns amount of experience
        public static BigInteger GetWrestlerExperience(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_experience_prefix.Concat(temp);
            var temp2 = Storage.Get(Storage.CurrentContext, key);
            return temp2.AsBigInteger();
        }

        public static BigInteger GetWrestlerLocation(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_location_prefix.Concat(temp);
            var temp2 = Storage.Get(Storage.CurrentContext, key);
            return temp2.AsBigInteger();
        }

        public static byte[] GetWrestlerScore(BigInteger wrestlerID)
        {
            var temp = wrestlerID.AsByteArray();
            var key = wrestler_score_prefix.Concat(temp);
            return Storage.Get(Storage.CurrentContext, key);
        }

        private static void UpdateWrestlerExperience(BigInteger wrestlerID, BigInteger amount)
        {
            var old_xp = GetWrestlerExperience(wrestlerID);
            var new_xp = old_xp + amount;
            if (new_xp> WRESTLER_MAX_XP)
            {
                new_xp = WRESTLER_MAX_XP;
            }

            if (old_xp != new_xp)
            {
                var temp = wrestlerID.AsByteArray();
                var key = wrestler_experience_prefix.Concat(temp);
                Storage.Put(Storage.CurrentContext, key, new_xp);
            }
        }

        private static BigInteger CalculateWrestlerStat(BigInteger level, BigInteger baseStat)
        {
            var result = (baseStat + 100) * 2;
            result *= level;
            result /= 100;
            result += 1;
            return result;
        }

        private static BigInteger CalculateWrestlerStamina(BigInteger level, BigInteger baseStamina)
        {
            BigInteger result = CalculateWrestlerStat(level, baseStamina);
            result = ((result * 2) / 7);
            result += 2;
            return result;
        }

        private static BigInteger CalculateWrestlerLevel(BigInteger XP)
        {
            return Sqrt(XP) / 61;
        }

        private static BigInteger CalculateDamage(BigInteger level, BigInteger atk, BigInteger def, BigInteger rnd, BigInteger power)
        {
            BigInteger result = ((2 * level) / 5) + 2;
            result *= power;
            result *= atk;
            result /= def;
            result /= 20;
            result += 2;

            // between 0 and 100
            BigInteger mod = 85 + (rnd % 16); 

            result *= mod;
            result /= 100;

            return result;
        }

        #endregion

        #region AUCTION API
        private static readonly byte[] auction_total_key = { (byte)'Z', (byte)'.', (byte)'T' };
        private static readonly byte[] auction_wrestler_prefix = { (byte)'Z', (byte)'.', (byte)'W' };
        private static readonly byte[] auction_price_prefix = { (byte)'Z', (byte)'.', (byte)'P' };

        // note: this should require GAS later
        public static bool AuctionWrestler(byte[] addressHash, BigInteger wrestlerID, BigInteger price)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return false;
            }

            var owner = GetWrestlerOwner(wrestlerID);
            if (owner != addressHash)
            {
                return false;
            }

            var wrestler_id_key = wrestlerID.AsByteArray();
            var key = wrestler_location_prefix.Concat(wrestler_id_key);

            var temp = Storage.Get(Storage.CurrentContext, key);
            BigInteger location = temp.AsBigInteger();
            if (location != WRESTLER_LOCATION_NONE)
            {
                return false;
            }

            // update wrestler location
            location = WRESTLER_LOCATION_AUCTION;
            Storage.Put(Storage.CurrentContext, key, location);

            // get auction pointer
            var lastAuctionIndex = GetAuctionWrestlerCount();

            // store wrestler ID  in auction slot
            key = auction_wrestler_prefix.Concat(lastAuctionIndex.AsByteArray());
            Storage.Put(Storage.CurrentContext, key, wrestlerID);

            // store price
            key = auction_price_prefix.Concat(lastAuctionIndex.AsByteArray());
            Storage.Put(Storage.CurrentContext, key, price);

            // increment and store auction pointerrr
            lastAuctionIndex = lastAuctionIndex + 1;
            Storage.Put(Storage.CurrentContext, auction_total_key, lastAuctionIndex);

            return true;
        }


        // note: This can also be used to remove from auction previously used wrestler, at zero GAS cost
        public static bool HireWrestler(byte[] addressHash, BigInteger auctionIndex)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return false;
            }

            byte[] temp;

            // fetch wrestler ID from auction slot
            var id_key = auction_wrestler_prefix.Concat(auctionIndex.AsByteArray());
            temp = Storage.Get(Storage.CurrentContext, id_key);
            BigInteger wrestlerID = temp.AsBigInteger();

            // fetch price
            var price_key = auction_price_prefix.Concat(auctionIndex.AsByteArray());
            temp = Storage.Get(Storage.CurrentContext, price_key);
            BigInteger hire_cost = temp.AsBigInteger();

            var wrestler_id_key = wrestlerID.AsByteArray();
            var location_key = wrestler_location_prefix.Concat(wrestler_id_key);

            // fetch wrestler state
            temp = Storage.Get(Storage.CurrentContext, location_key);
            BigInteger location = temp.AsBigInteger();

            if (location != WRESTLER_LOCATION_AUCTION)
            {
                return false;
            }

            var owner = GetWrestlerOwner(wrestlerID);
            if (owner != addressHash)
            {
                // update account balance
                if (!UseAccountBalance(addressHash, hire_cost))
                {
                    return false;
                }
            }

            // revert wrestler state to default
            Storage.Delete(Storage.CurrentContext, location_key);

            // update wrestler owner
            var owner_key = wrestler_owner_prefix.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, owner_key, addressHash);

            // get auction pointer and decrement
            var lastAuctionIndex = GetAuctionWrestlerCount();
            lastAuctionIndex = lastAuctionIndex - 1;
            Storage.Put(Storage.CurrentContext, auction_total_key, lastAuctionIndex);

            // remove last wrestler ID slot
            var auction_wrestler_key = auction_wrestler_prefix.Concat(lastAuctionIndex.AsByteArray());
            temp = Storage.Get(Storage.CurrentContext, auction_wrestler_key);
            Storage.Delete(Storage.CurrentContext, auction_wrestler_key);

            // move it to unused slot
            auction_wrestler_key = auction_wrestler_prefix.Concat(auctionIndex.AsByteArray());
            Storage.Put(Storage.CurrentContext, auction_wrestler_key, temp);

            return true;
        }


        // returns how many wrestlers are availble for auction
        public static BigInteger GetAuctionWrestlerCount()
        {
            var temp = Storage.Get(Storage.CurrentContext, auction_total_key);
            return temp.AsBigInteger();
        }

        // returns wrestlerID at specified auction index
        public static BigInteger GetAuctionWrestlerByIndex(BigInteger index)
        {
            var key = auction_wrestler_prefix.Concat(index.AsByteArray());
            var temp = Storage.Get(Storage.CurrentContext, key);
            return temp.AsBigInteger();
        }
        #endregion

        #region GYM API
        // note - this should require a bit of GAS
        public static string StartTrainingWrestler(byte[] addressHash, BigInteger wrestlerID, int mode)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return "witness";
            }

            // check if theres any gas available
            if (!UpdateAccountBalance(addressHash))
            {
                return "balance";
            }

            var wrestler_id_key = wrestlerID.AsByteArray();

            var owner_key = wrestler_owner_prefix.Concat(wrestler_id_key);
            var owner = Storage.Get(Storage.CurrentContext, owner_key);
            if (owner != addressHash)
            {
                return "owner";
            }

            var location_key = wrestler_location_prefix.Concat(wrestler_id_key);

            var temp = Storage.Get(Storage.CurrentContext, location_key);
            BigInteger location = temp.AsBigInteger();

            if (location != WRESTLER_LOCATION_NONE)
            {
                return "location";
            }

            var time_key = wrestler_timestamp_prefix.Concat(wrestler_id_key);
            var temp2 = Storage.Get(Storage.CurrentContext, time_key);
            BigInteger last_time = temp2.AsBigInteger();
            BigInteger current_time = Runtime.Time;

            var diff = current_time - last_time;
            if (diff < seconds_per_day)
            {
                return "time";
            }

            // update wrestler location
            location = WRESTLER_LOCATION_GYM;
            Storage.Put(Storage.CurrentContext, location_key, location);

            Storage.Put(Storage.CurrentContext, time_key, current_time);

            return "ok";
        }

        // note - requires that wrestler spent at least one hour training
        public static string EndTrainingWrestler(byte[] addressHash, BigInteger wrestlerID)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return "witness";
            }

            // check if theres any gas available
            if (!UpdateAccountBalance(addressHash))
            {
                return "balance";
            }

            var wrestler_id_key = wrestlerID.AsByteArray();

            var owner_key = wrestler_owner_prefix.Concat(wrestler_id_key);
            var owner = Storage.Get(Storage.CurrentContext, owner_key);
            if (owner != addressHash)
            {
                return "owner";
            }

            var location_key = wrestler_location_prefix.Concat(wrestler_id_key);

            var temp = Storage.Get(Storage.CurrentContext, location_key);
            BigInteger location = temp.AsBigInteger();

            if (location != WRESTLER_LOCATION_GYM)
            {
                return "location";
            }

            var time_key = wrestler_timestamp_prefix.Concat(wrestler_id_key);
            var temp2 = Storage.Get(Storage.CurrentContext, time_key);
            BigInteger last_time = temp2.AsBigInteger();
            BigInteger current_time = Runtime.Time;

            var xp_key = wrestler_experience_prefix.Concat(wrestler_id_key);
            var temp3 = Storage.Get(Storage.CurrentContext, xp_key);
            BigInteger current_xp = temp3.AsBigInteger();

            // calculate elapsed XP
            // one XP point is earned per second inside gym
            // max is 3600 XP per session. One session per day max
            var xp_amount = current_time - last_time;
            if (xp_amount > seconds_per_hour)
            {
                xp_amount = seconds_per_hour;
            }

            if (current_xp + xp_amount > WRESTLER_MAX_XP)
            {
                xp_amount = WRESTLER_MAX_XP - current_xp;
            }

            // around 0.1 GAS per one hour of training
            var gas_cost = (xp_amount * gas_decimals) / (seconds_per_hour * 10);
            if (!UseAccountBalance(addressHash, gas_cost))
            {
                return "gas";
            }

            current_xp += xp_amount;
            Storage.Put(Storage.CurrentContext, xp_key, current_xp);
            Storage.Put(Storage.CurrentContext, time_key, current_time);

            // reset wrestler location
            Storage.Delete(Storage.CurrentContext, location_key);

            return "ok";

        }
        #endregion

        #region BATTLE API
        private static readonly byte[] queue_wrestler_key = { (byte)'Q', (byte)'.', (byte)'W' };
        private static readonly byte[] queue_timestamp_key = { (byte)'Q', (byte)'.', (byte)'T' };

        private static readonly byte[] battle_total_key = { (byte)'B', (byte)'.', (byte)'T' };
        private static readonly byte[] battle_opponent_prefix = { (byte)'B', (byte)'.', (byte)'O' };
        private static readonly byte[] battle_state_prefix = { (byte)'B', (byte)'.', (byte)'S' };
        private static readonly byte[] battle_winner_prefix = { (byte)'B', (byte)'.', (byte)'W' };
        private static readonly byte[] battle_stamina_prefix = { (byte)'B', (byte)'.', (byte)'Z' };
        private static readonly byte[] battle_move_prefix = { (byte)'B', (byte)'.', (byte)'M' };
        private static readonly byte[] battle_damage_prefix = { (byte)'B', (byte)'.', (byte)'D' };

        private static readonly byte BATTLE_STATE_INIT = 0;
        private static readonly byte BATTLE_STATE_WAITING = 1;
        private static readonly byte BATTLE_STATE_READY = 2;
        private static readonly byte BATTLE_STATE_WIN = 3;
        private static readonly byte BATTLE_STATE_DRAW = 4;

        private static readonly byte BATTLE_MOVE_IDLE = 0;
        private static readonly byte BATTLE_MOVE_ATTACK = 1;
        private static readonly byte BATTLE_MOVE_SMASH = 2;
        private static readonly byte BATTLE_MOVE_COUNTER = 3;
        private static readonly byte BATTLE_MOVE_BLOCK = 4;

        private static readonly byte DAMAGE_BASE_POWER = 10;
        private static readonly byte DAMAGE_SMASH_POWER = 25;

        // note - this should require a bit of GAS
        public static string QueueWrestler(byte[] addressHash, BigInteger wrestlerID)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return "witness";
            }

            var wrestler_id_key = wrestlerID.AsByteArray();

            var temp2 = Storage.Get(Storage.CurrentContext, queue_wrestler_key);
            BigInteger other_wrestlerID = temp2.AsBigInteger();

            if (other_wrestlerID == 0)
            {
                // no one in queue yet, so add this wrestler there

                var location_key = wrestler_battle_prefix.Concat(wrestler_id_key);
                var location = WRESTLER_LOCATION_QUEUE;
                Storage.Put(Storage.CurrentContext, location_key, location);

                Storage.Put(Storage.CurrentContext, queue_wrestler_key, wrestlerID);

                return "ok";
            }

            // cant battle against itself
            if (other_wrestlerID.Equals(wrestlerID))
            {
                return "same";
            }

            // can only use ownned wrestlers to battle, not from other players
            var owner_key_A = wrestler_owner_prefix.Concat(wrestler_id_key);
            var owner = Storage.Get(Storage.CurrentContext, owner_key_A);
            if (owner != addressHash)
            {
                return "owner";
            }

            // cant have battles between two wrestlers of same owner
            var other_wrestler_id_key = other_wrestlerID.AsByteArray();
            var owner_key_B = wrestler_owner_prefix.Concat(other_wrestler_id_key);
            var other_owner = Storage.Get(Storage.CurrentContext, owner_key_B);
            if (owner.AsBigInteger() == other_owner.AsBigInteger())
            {
                return "sameown";
            }

            // get last battle ID, increment and update
            var temp3 = Storage.Get(Storage.CurrentContext, battle_total_key);
            BigInteger battle_id = temp3.AsBigInteger();
            battle_id = battle_id + 1;
            Storage.Put(Storage.CurrentContext, battle_total_key, battle_id);

            {
                var location = WRESTLER_LOCATION_BATTLE;

                // update battle ID and location of first wrestler
                var battle_key_A = wrestler_battle_prefix.Concat(wrestler_id_key);
                Storage.Put(Storage.CurrentContext, battle_key_A, battle_id);
                var location_key_A = wrestler_battle_prefix.Concat(wrestler_id_key);
                Storage.Put(Storage.CurrentContext, location_key_A, location);

                // update battle ID and location of second wrestler
                var battle_key_B = wrestler_battle_prefix.Concat(other_wrestler_id_key);
                Storage.Put(Storage.CurrentContext, battle_key_B, battle_id);
                var location_key_B = wrestler_battle_prefix.Concat(other_wrestler_id_key);
                Storage.Put(Storage.CurrentContext, location_key_B, location);
            }

            // save opponent keys
            var battle_id_key = battle_id.AsByteArray();
            var opponent_key = battle_opponent_prefix.Concat(battle_id_key);

            var opponent_key_A = opponent_key.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, opponent_key_A, other_wrestlerID);

            var opponent_key_B = opponent_key.Concat(other_wrestler_id_key);
            Storage.Put(Storage.CurrentContext, opponent_key_B, wrestlerID);

            return "ok";
        }

        public static string StartBattle(byte[] addressHash, BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var state_key = battle_state_prefix.Concat(battle_id_key);
            var temp = Storage.Get(Storage.CurrentContext, state_key);
            var battle_state = temp.AsBigInteger();

            if (battle_state != BATTLE_STATE_INIT)
            {
                return "uninit";
            }

            var wrestler_id_key = wrestlerID.AsByteArray();

            // check if wrestler belongs to caller
            var owner_key = wrestler_owner_prefix.Concat(wrestler_id_key);
            var owner = Storage.Get(Storage.CurrentContext, owner_key);
            if (owner != addressHash)
            {
                return "owner";
            }

            var opponent_key = battle_opponent_prefix.Concat(battle_id_key);

            var opponent_key_A = opponent_key.Concat(wrestler_id_key);
            var temp2 = Storage.Get(Storage.CurrentContext, opponent_key_A);
            var other_wrestlerID = temp2.AsBigInteger();

            if (other_wrestlerID == 0)
            {
                return "unwrestler";
            }

            battle_state = BATTLE_STATE_WAITING;
            Storage.Put(Storage.CurrentContext, state_key, battle_state);

            InitWrestlerStamina(battleID, wrestlerID);
            InitWrestlerStamina(battleID, other_wrestlerID);

            return "ok";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BigInteger InitWrestlerStamina(BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var wrestler_id_key = wrestlerID.AsByteArray();
            var stats_key_A = wrestler_stats_prefix.Concat(wrestler_id_key);
            var stats_A = Storage.Get(Storage.CurrentContext, stats_key_A);

            var XP_A = GetWrestlerExperience(wrestlerID);
            var level_A = CalculateWrestlerLevel(XP_A);
            BigInteger seed = stats_A.AsBigInteger();

            var base_sta_A = seed % 64;
            var stamina_A = CalculateWrestlerStamina(level_A, base_sta_A);

            var stamina_key = battle_stamina_prefix.Concat(battle_id_key);

            var stamina_key_A = stamina_key.Concat(wrestler_id_key);
            Storage.Put(Storage.CurrentContext, stamina_key_A, stamina_A);

            return stamina_A;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BigInteger GetWrestlerStamina(BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var wrestler_id_key = wrestlerID.AsByteArray();

            var stamina_key = battle_stamina_prefix.Concat(battle_id_key);

            var stamina_key_A = stamina_key.Concat(wrestler_id_key);
            var temp = Storage.Get(Storage.CurrentContext, stamina_key_A);
            return temp.AsBigInteger();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BigInteger GetWrestlerMove(BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var wrestler_id_key = wrestlerID.AsByteArray();

            var move_key = battle_move_prefix.Concat(battle_id_key);
            
            var move_key_A = move_key.Concat(wrestler_id_key);
            var temp = Storage.Get(Storage.CurrentContext, move_key_A);
            return temp.AsBigInteger();
        }

        public static string AdvanceBattle(byte[] addressHash, BigInteger battleID, BigInteger wrestlerID)
        {
            if (!Runtime.CheckWitness(addressHash))
            {
                return "witness";
            }

            var battle_id_key = battleID.AsByteArray();
            var state_key = battle_state_prefix.Concat(battle_id_key);
            var temp = Storage.Get(Storage.CurrentContext, state_key);
            var battle_state = temp.AsBigInteger();

            if (battle_state != BATTLE_STATE_READY)
            {
                return "unready";
            }

            var wrestler_key_A = wrestlerID.AsByteArray();

            // can only use ownned wrestlers to battle, not from other players
            var owner_key = wrestler_owner_prefix.Concat(wrestler_key_A);
            var owner = Storage.Get(Storage.CurrentContext, owner_key);
            if (owner != addressHash)
            {
                return "owner";
            }
            
            var opponent_key = battle_opponent_prefix.Concat(battle_id_key);

            var opponent_key_A = opponent_key.Concat(wrestler_key_A);
            var temp2 = Storage.Get(Storage.CurrentContext, opponent_key_A);
            var other_wrestlerID = temp2.AsBigInteger();

            if (other_wrestlerID == 0)
            {
                return "unwrestler";
            }

            var wrestler_key_B = other_wrestlerID.AsByteArray();

            BigInteger seed;

            BigInteger atk_B, def_B, stamina_B, move_B, level_B;
            {
                var stats_key_B = wrestler_stats_prefix.Concat(wrestler_key_B);
                var stats_B = Storage.Get(Storage.CurrentContext, stats_key_B);

                var XP_B = GetWrestlerExperience(other_wrestlerID);
                level_B = CalculateWrestlerLevel(XP_B);
                seed = stats_B.AsBigInteger();

                var base_sta_B = seed % 64;
                seed = NextRandom(seed);
                var base_atk_B = seed % 64;
                seed = NextRandom(seed);
                var base_def_B = seed % 64;

                stamina_B = GetWrestlerStamina(battleID, other_wrestlerID);
                atk_B = CalculateWrestlerStat(level_B, base_atk_B);
                def_B = CalculateWrestlerStat(level_B, base_def_B);
                move_B = GetWrestlerMove(battleID, other_wrestlerID);
            }

            BigInteger atk_A, def_A, stamina_A, move_A, level_A;
            {
                var stats_key_A = wrestler_stats_prefix.Concat(wrestler_key_A);
                var stats_A = Storage.Get(Storage.CurrentContext, stats_key_A);

                var XP_A = GetWrestlerExperience(wrestlerID);
                level_A = CalculateWrestlerLevel(XP_A);
                seed = stats_A.AsBigInteger();

                var base_sta_A = seed % 64;
                seed = NextRandom(seed);
                var base_atk_A = seed % 64;
                seed = NextRandom(seed);
                var base_def_A = seed % 64;

                stamina_A = GetWrestlerStamina(battleID, wrestlerID);
                atk_A = CalculateWrestlerStat(level_A, base_atk_A);
                def_A = CalculateWrestlerStat(level_A, base_def_A);
                move_A = GetWrestlerMove(battleID, wrestlerID);
            }

            seed = Randomize(battle_id_key);

            BigInteger damage_A;
            BigInteger damage_B;

            if (move_A == BATTLE_MOVE_SMASH)
            {
                damage_A = CalculateDamage(level_A, atk_A, def_B, seed, DAMAGE_SMASH_POWER);
                seed = NextRandom(seed);

                if (move_B == BATTLE_MOVE_COUNTER)
                {
                    damage_B = damage_A;
                    damage_A = 0;
                }
                else
                if (move_B == BATTLE_MOVE_BLOCK)
                {
                    damage_A /= 2;
                    damage_B = damage_A;
                }
                else
                if (move_B == BATTLE_MOVE_ATTACK)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_BASE_POWER);
                }
                else
                {
                    damage_B = 0;
                }
            }
            else
            if (move_A == BATTLE_MOVE_COUNTER)
            {
                damage_A = 0;

                if (move_B == BATTLE_MOVE_SMASH)
                {
                    damage_A = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_SMASH_POWER);
                    damage_B = 0;
                }
                else
                if (move_B == BATTLE_MOVE_ATTACK)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_BASE_POWER);
                }
                else
                {
                    damage_B = 0;
                }
            }
            else
            if (move_A == BATTLE_MOVE_ATTACK)
            {
                damage_A = CalculateDamage(level_A, atk_A, def_B, seed, DAMAGE_BASE_POWER);
                seed = NextRandom(seed);

                if (move_B == BATTLE_MOVE_SMASH)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_SMASH_POWER);
                }
                else
                if (move_B == BATTLE_MOVE_BLOCK)
                {
                    damage_A /= 2;
                    damage_B = damage_A;
                }
                else
                {
                    damage_B = 0;
                }
            }
            else
            if (move_A == BATTLE_MOVE_BLOCK)
            {
                damage_A = 0;

                if (move_B == BATTLE_MOVE_SMASH)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_SMASH_POWER);
                    damage_B /= 2;
                    damage_A = damage_B;
                }
                else
                if (move_B == BATTLE_MOVE_ATTACK)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_BASE_POWER);
                    damage_B /= 2;
                    damage_A = damage_B;
                }
                else
                {
                    damage_B = 0;
                }
            }
            else
            {
                damage_A = 0;

                if (move_B == BATTLE_MOVE_SMASH)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_SMASH_POWER);
                }
                else
                if (move_B == BATTLE_MOVE_ATTACK)
                {
                    damage_B = CalculateDamage(level_B, atk_B, def_A, seed, DAMAGE_BASE_POWER);
                }
                else
                {
                    damage_B = 0;
                }
            }

            stamina_A -= damage_A;
            stamina_B -= damage_B;

            // check for end of battle conditions
            if (stamina_A <= 0 && stamina_B <= 0)
            {
                battle_state = BATTLE_STATE_DRAW;
                Storage.Put(Storage.CurrentContext, state_key, battle_state);
            }
            else
            if (stamina_A <= 0 && stamina_B > 0)
            {
                battle_state = BATTLE_STATE_WIN;
                Storage.Put(Storage.CurrentContext, state_key, battle_state);

                var winner_key = battle_winner_prefix.Concat(battle_id_key);
                Storage.Put(Storage.CurrentContext, winner_key, other_wrestlerID);
            }
            else
            if (stamina_A > 0 && stamina_B <= 0)
            {
                battle_state = BATTLE_STATE_WIN;
                Storage.Put(Storage.CurrentContext, state_key, battle_state);

                var winner_key = battle_winner_prefix.Concat(battle_id_key);
                Storage.Put(Storage.CurrentContext, winner_key, wrestlerID);
            }
            else
            {
                battle_state = BATTLE_STATE_WAITING;
                Storage.Put(Storage.CurrentContext, state_key, battle_state);

                var move_key = battle_move_prefix.Concat(battle_id_key);

                // remove moves from storage
                var move_key_A = move_key.Concat(wrestler_key_A);
                Storage.Delete(Storage.CurrentContext, move_key_A);

                var move_key_B = move_key.Concat(wrestler_key_B);
                Storage.Delete(Storage.CurrentContext, move_key_B);
            }

            // update turn damages
            var damage_key = battle_damage_prefix.Concat(battle_id_key);

            var damage_key_A = damage_key.Concat(wrestler_key_A);
            if (damage_A > 0)
            {
                Storage.Put(Storage.CurrentContext, damage_key_A, damage_A);
            }
            else
            {
                Storage.Delete(Storage.CurrentContext, damage_key_A);
            }

            var damage_key_B = damage_key.Concat(wrestler_key_B);
            if (damage_B > 0)
            {
                Storage.Put(Storage.CurrentContext, damage_key_B, damage_B);
            }
            else
            {
                Storage.Delete(Storage.CurrentContext, damage_key_B);
            }

            return "ok";
        }

        public static string SetBattleMove (byte[] addressHash, BigInteger battleID, BigInteger wrestlerID, BigInteger moveID)
        {
            if (moveID < 0 || moveID > 4)
            {
                return "invalid";
            }

            if (!Runtime.CheckWitness(addressHash))
            {
                return "witness";
            }

            var battle_id_key = battleID.AsByteArray();
            var state_key = battle_state_prefix.Concat(battle_id_key);
            var temp = Storage.Get(Storage.CurrentContext, state_key);
            var battle_state = temp.AsBigInteger();

            if (battle_state != BATTLE_STATE_WAITING)
            {
                return "unwait";
            }

            var wrestler_key_A = wrestlerID.AsByteArray();

            // can only use ownned wrestlers to battle, not from other players
            var owner_key = wrestler_owner_prefix.Concat(wrestler_key_A);
            var owner = Storage.Get(Storage.CurrentContext, owner_key);
            if (owner != addressHash)
            {
                return "owner";
            }

            var opponent_key = battle_opponent_prefix.Concat(battle_id_key);

            var opponent_key_A = opponent_key.Concat(wrestler_key_A);
            var temp2 = Storage.Get(Storage.CurrentContext, opponent_key_A);
            var other_wrestlerID = temp2.AsBigInteger();

            if (other_wrestlerID == 0)
            {
                return "unwrestler";
            }

            var move_key = battle_move_prefix.Concat(battle_id_key);
            var move_key_A = move_key.Concat(wrestler_key_A);

            var temp3 = Storage.Get(Storage.CurrentContext, move_key_A);
            var cur_move = temp3.AsBigInteger();
            if (cur_move != 0)
            {
                return "already";
            }

            Storage.Put(Storage.CurrentContext, move_key_A, moveID);

            var wrestler_key_B = other_wrestlerID.AsByteArray();
            var move_key_B = move_key.Concat(wrestler_key_B);
            var temp4 = Storage.Get(Storage.CurrentContext, move_key_B);
            BigInteger opponent_move = temp4.AsBigInteger();

            // if both players put their move, then advance to next stace
            if (opponent_move != 0)
            {
                battle_state = BATTLE_STATE_READY;
                Storage.Put(Storage.CurrentContext, state_key, battle_state);
            }

            return "ok";
        }

        public static BigInteger GetBattleDamage(BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var wrestler_key = wrestlerID.AsByteArray();

            var damage_key = battle_damage_prefix.Concat(battle_id_key);
            var damage_key_A = damage_key.Concat(wrestler_key);
            var temp = Storage.Get(Storage.CurrentContext, damage_key_A);
            return temp.AsBigInteger();
        }

        public static BigInteger GetBattleMove(BigInteger battleID, BigInteger wrestlerID)
        {
            var battle_id_key = battleID.AsByteArray();
            var wrestler_key = wrestlerID.AsByteArray();

            var damage_key = battle_move_prefix.Concat(battle_id_key);
            var damage_key_A = damage_key.Concat(wrestler_key);
            var temp = Storage.Get(Storage.CurrentContext, damage_key_A);
            return temp.AsBigInteger();
        }

        public static BigInteger GetBattleState(BigInteger battleID)
        {
            var battle_id_key = battleID.AsByteArray();
            var state_key = battle_damage_prefix.Concat(battle_id_key);
            var temp = Storage.Get(Storage.CurrentContext, state_key);
            return temp.AsBigInteger();
        }

        #endregion

    }
}
