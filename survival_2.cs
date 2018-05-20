using System;
using System.Collections.Generic;

namespace MCGalaxy
{
    public class Survival_2 : Plugin
    {
        public override string name { get { return "survival_2"; } }
        public override string website { get { return "www.example.com"; } }
        public override string MCGalaxy_Version { get { return "1.9.0.2"; } }
        public override int build { get { return 100; } }
        public override string welcome { get { return "Survival Plugin loaded !"; } }
        public override string creator { get { return "Sirvoid and Odd0002"; } }
        public override bool LoadAtStartup { get { return true; } }

        private Dictionary<Player, SurvivalPlayer> playerMap = new Dictionary<Player, SurvivalPlayer>();

        public override void Help(Player p)
        {
            Helpers.SendTextBlockToPlayer(p, 
                "%7-----%aSurvival Plugin by Sirvoid%7-----\n" + 
                "%eFor more informations about crafting, type %a/craft\n" +
                "%eTo see your inventory, type %a/inventory\n" +
                "%eTo delete items that you're holding, %a/drop <quantity>\n" +
                "%eTo go home, %a/home\n" +
                "%7-------------------------------");
        }

        public override void Load(bool startup)
        {

        }

        public override void Unload(bool shutdown)
        {
            //throw new NotImplementedException();
        }

        

    }

    public struct CraftingRecipe
    {
        public string name;
        public string category;
        public int craftCount;
        public int itemID;
        public Dictionary<int, int> requiredIngredients;
    }
    

    public class SurvivalPlayer
    {
        Player thisPlayer;
        int health;
        int breath;
        int pvpCooldown;

        public SurvivalPlayer(Player player)
        {
            this.thisPlayer = player;
        }

        public void SendMessage(string message)
        {
            thisPlayer.SendMessage(message);
        }

        //TODO see if we need this method
        public void SendTextBlockToPlayer(string message)
        {
            string[] messages = message.Split('\n');
            foreach (string msg in messages)
            {
                thisPlayer.SendMessage(msg);
            }
        }
    }

    public class Inventory
    {

    }

    public static class Helpers
    {
        public static void SendTextBlockToPlayer(Player p, string message)
        {
            string[] messages = message.Split('\n');
            foreach (string msg in messages)
            {
                p.SendMessage(msg);
            }
        }

        public static Dictionary<string, CraftingRecipe> loadCraftsFromFile(string filename)
        {
            Dictionary<string, CraftingRecipe> crafts = new Dictionary<string, CraftingRecipe>();
            string currLine;
            System.IO.StreamReader file = new System.IO.StreamReader(filename);
            while ((currLine = file.ReadLine()) != null)
            {
                //Craft info stored as: name,category,ID,quantity,ingredient1ID,ingredient1Count,ingredient2ID,ingredient2Count,... 
                string[] craftInfo = currLine.Split(',');
                crafts[craftInfo[0]] = createRecipie(craftInfo);
            }
            return crafts;
        }


        public static CraftingRecipe createRecipie(string[] craftInfo)
        {
            int offset = 4;
            CraftingRecipe recipe;

            recipe.name = craftInfo[0];
            recipe.category = craftInfo[1];
            recipe.itemID = Int32.Parse(craftInfo[2]);
            recipe.craftCount = Int32.Parse(craftInfo[3]);
            recipe.category = craftInfo[1];

            int craftCount = craftInfo.Length;

            Dictionary<int, int> requiredIngredients = new Dictionary<int, int> ();
            for (int i=offset; i < craftCount; i+=2)
            {
                int ingredient = Int32.Parse(craftInfo[i]);
                int count = Int32.Parse(craftInfo[i + 1]);
                requiredIngredients[ingredient] = count;
            }

            recipe.requiredIngredients = requiredIngredients;

            return recipe;
        }
    }
}