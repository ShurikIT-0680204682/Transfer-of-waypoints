using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Transfer_of_waypoints
{
    public class Transfer_of_waypointsModSystem : ModSystem
    {
        private ICoreClientAPI api;
        private bool isImporting = false;  // Флаг для відправки точок
        private List<string[]> filteredLines = new List<string[]>();  // Список точок
        private int currentIndex = 0;  // Індекс для наступної точки
        //private int delayValue = 1050;
        // Шлях до файлів
        private static string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Формуємо повний шлях
        string outputPath = Path.Combine(userFolderPath, "VintagestoryData", "filtered_waypoints.txt");


        // Перша команда: записуємо точки у файл
        public override void StartClientSide(ICoreClientAPI api)
        {
            this.api = api;  // Зберігаємо доступ до api

            api.ChatCommands
                .Create("import")
                .WithDescription("unloads waypoints from a buffer file")
                 .WithArgs(api.ChatCommands.Parsers.Int("int_delay"))
                .HandleWith(importWp);

            api.ChatCommands
                .Create("export")
                .WithDescription("loads waypoints into a buffer file")
                .HandleWith(exportWp);

            api.ChatCommands
                .Create("wpchat")
                .WithDescription("waypoint search by name or number in the list")

                .BeginSubCommand("all")
                        .WithDescription("Message in global chat")
                        .RequiresPlayer()
                        .WithArgs(api.ChatCommands.Parsers.OptionalAll("id_and_name"))
                        .HandleWith(wpchatall)
                    .EndSubCommand()

                     .BeginSubCommand("me")
                        .WithDescription("Message in local chat")
                        .RequiresPlayer()
                        .WithArgs(api.ChatCommands.Parsers.OptionalAll("id_and_name"))
                        .HandleWith(wpchatme)
                    .EndSubCommand()
                     ;




        }

        // Перша команда - записуємо точки з client-chat.log в filtered_waypoints.txt
        private TextCommandResult exportWp(TextCommandCallingArgs args)
        {


            if (isImporting)
            {
                return TextCommandResult.Success("Export is already active.");
            }

            isImporting = true;
            filteredLines.Clear();  // Очищаємо попередні точки


            string inputFilePath = Path.Combine(userFolderPath, "VintagestoryData/Logs", "client-chat.log");
            try
            {
                using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    string line;
                    bool isInBlock = false;
                    string startWord = Lang.Get("transferofwaypoints:start_copying"); // Початкове слово
                    string endSymbol = "@"; // Кінцевий символ

                    while ((line = reader.ReadLine()) != null)
                    {
                        // Початок блоку
                        if (line.Contains(startWord) || line.Contains("waypoints:"))
                        {
                            isInBlock = true;
                        }

                        if (isInBlock)
                        {
                            filteredLines.Add(new string[] { line });  // Додаємо рядок до списку

                            // Кінець блоку
                            if (line.Contains(endSymbol))
                            {
                                isInBlock = false;
                                break; // Завершуємо після досягнення кінця блоку
                            }
                        }
                    }
                    if (filteredLines.Count == 0)
                    {
                        return TextCommandResult.Error("No waypoints or no previous command entered.");
                    }
                }

                // Перезаписуємо файл з новими даними
                using (StreamWriter writer = new StreamWriter(outputPath, false))
                {
                    foreach (string[] filteredLine in filteredLines)
                    {
                        // Перетворюємо масив на рядок перед записом
                        string line = string.Join(" ", filteredLine);
                        writer.WriteLine(line);
                    }
                }
                isImporting = false;
                return TextCommandResult.Success("The data is stored in the file: " + outputPath);

            }
            catch (FileNotFoundException)
            {
                return TextCommandResult.Error("Файл логів не знайдено.");
            }
            catch (IOException ex)
            {
                return TextCommandResult.Error("Помилка доступу до файлу: " + ex.Message);
            }
            catch (Exception ex)
            {
                return TextCommandResult.Error("Невідома помилка: " + ex.Message);
            }
            finally
            {
                isImporting = false;
            }
        }

        // Друга команда - імпортуємо точки з filtered_waypoints.txt і відправляємо їх в чат
        private TextCommandResult importWp(TextCommandCallingArgs args)
        {

            int delayValue = (int)args.Parsers[0].GetValue();
            if (delayValue < 1060)
            {
                delayValue = 1060;
            }

            if (isImporting)
            {
                return TextCommandResult.Success("\r\nImport is already active.");
            }

            isImporting = true;
            currentIndex = 0;  // Скидаємо індекс

            // Завантажуємо точки з файлу
            LoadWaypointsFromFile(outputPath);

            // Починаємо відправку точок
            api.World.RegisterGameTickListener(SendWaypoint, delayValue);  // Відправляти кожну секунду (1000 мс)

            return TextCommandResult.Success("Імпорт точок почався.");
        }

        // Завантажуємо точки з файлу
        private void LoadWaypointsFromFile(string outputPath)
        {
            filteredLines.Clear();  // Очищаємо список попередніх точок

            using (StreamReader reader = new StreamReader(outputPath))
            {
                reader.ReadLine();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Шукаємо точку після слова "at"
                    int atIndex = line.IndexOf("at");
                    if (atIndex > 0)
                    {
                        string title = line.Substring(2, atIndex - 3).Trim();  // Витягуємо назву точки (відразу після номера)
                        string coordinates = line.Substring(atIndex + 3).Trim();  // Витягуємо координати

                        filteredLines.Add(new string[] { title, coordinates });
                    }
                }
            }
        }

        // Відправка точок у чат
        private void SendWaypoint(float dt)
        {
            if (!isImporting || filteredLines.Count == 0)
                return;

            if (currentIndex < filteredLines.Count)
            {
                var waypoint = filteredLines[currentIndex];

                // Формуємо команду для чату
                string message = $"/waypoint addati circle {waypoint[1]} false white {waypoint[0]}";

                // Відправляємо команду в чат
                api.SendChatMessage(message);
                currentIndex++;  // Переходимо до наступної точки
            }
            else
            {
                // Якщо всі точки відправлені, завершимо процес
                isImporting = false;
                api.ShowChatMessage("\r\nAll points are imported.");
            }
        }

        private TextCommandResult wpchatall(TextCommandCallingArgs args)
        {
            LoadWaypointsFromFile(outputPath);

            // Перевірка на наявність аргументу

            if (args[0] == null)
            {
                return TextCommandResult.Error("Argument missing");
            }
            string input = args[0] as string;
            int targetIndex = 0;

            // Перевірка, чи є input числом
            if (int.TryParse(input, out targetIndex))
            {
                // Якщо input це число, шукаємо за індексом
                if (targetIndex >= 0 && targetIndex < filteredLines.Count)
                {
                    var waypoint = filteredLines[targetIndex];
                    string message = $"{targetIndex} {waypoint[0]} {waypoint[1]}"; // Формуємо повідомлення
                    api.SendChatMessage(message); // Виводимо в чат
                }
                else
                {
                    api.ShowChatMessage("Index out of range.");
                }
            }
            else
            {


                if (args.Parsers.Count == 0)
                {
                    return TextCommandResult.Error("\r\nEnter the name of the point!");
                }

                string searchTerm = args[0].ToString().ToLower(); // Отримуємо аргумент і переводимо в нижній регістр
                List<string> results = new List<string>();
                int indexVal = 0;
                foreach (var waypoint in filteredLines)
                {
                    if (waypoint.Length >= 2)  // Переконуємося, що масив містить хоча б 2 елементи
                    {

                        if (waypoint[0].ToLower().Contains(searchTerm))  // Шукаємо по імені
                        {
                            results.Add($"{indexVal}: {waypoint[0]} {waypoint[1]}");
                        }
                    }
                    indexVal++;
                }

                if (results.Count == 0)
                {
                    return TextCommandResult.Success("Точка не знайдена.");
                }

                int index = 0;
                int delayMs = 1500;  // Затримка 1 секунда (1000 мс)

                api.World.RegisterGameTickListener((dt) =>
                {
                    if (index < results.Count)
                    {
                        api.SendChatMessage(results[index]);
                        index++;
                    }
                }, delayMs);
            }

            return TextCommandResult.Success();
        }


        private TextCommandResult wpchatme(TextCommandCallingArgs args)
        {
            LoadWaypointsFromFile(outputPath);

            if (args[0] == null)
            {
                return TextCommandResult.Error("Argument missing");
            }

            string input = args[0] as string;
            int targetIndex = 0;

            // Перевірка, чи є input числом
            if (int.TryParse(input, out targetIndex))
            {
                // Якщо input це число, шукаємо за індексом
                if (targetIndex >= 0 && targetIndex < filteredLines.Count)
                {
                    var waypoint = filteredLines[targetIndex];
                    string message = $"{targetIndex}: {waypoint[0]} {waypoint[1]}"; // Формуємо повідомлення
                    api.ShowChatMessage(message); // Виводимо в чат
                }
                else
                {
                    api.ShowChatMessage("Index out of range.");
                }
            }
            else
            {


                if (args.Parsers.Count == 0)
                {
                    return TextCommandResult.Error("\r\nEnter the name of the point!");
                }

                string searchTerm = args[0].ToString().ToLower(); // Отримуємо аргумент і переводимо в нижній регістр
                List<string> results = new List<string>();
                int indexVal = 0;

                foreach (var waypoint in filteredLines)
                {
                    if (waypoint.Length >= 2)  // Переконуємося, що масив містить хоча б 2 елементи
                    {

                        if (waypoint[0].ToLower().Contains(searchTerm))  // Шукаємо по імені
                        {
                            results.Add($"{indexVal}: {waypoint[0]} {waypoint[1]}");
                        }
                    }
                    indexVal++;
                }

                if (results.Count == 0)
                {
                    return TextCommandResult.Success("Point not found.");
                }

                int index = 0;

                while (index < results.Count)
                {

                    api.ShowChatMessage(results[index]);
                    index++;

                }



            }
            return TextCommandResult.Success();
        }
    }
}

