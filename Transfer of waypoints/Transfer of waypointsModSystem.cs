using System;
using System.IO;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace Transfer_of_waypoints
{
    public class Transfer_of_waypointsModSystem : ModSystem
    {
        private ICoreClientAPI api;
        private bool isImporting = false;  // Флаг для відправки точок
        private List<string[]> filteredLines = new List<string[]>();  // Список точок
        private int currentIndex = 0;  // Індекс для наступної точки

        // Шлях до файлів
        private static string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Формуємо повний шлях
        string outputPath = Path.Combine(userFolderPath, "VintagestoryData", "filtered_waypoints.txt");
       

        // Перша команда: записуємо точки у файл
        public override void StartClientSide(ICoreClientAPI api)
        {
            this.api = api;  // Зберігаємо доступ до api

           // api.RegisterTranslation("en", "assets/transferofwaypoints/lang/en.json");
           // api.RegisterTranslation("uk", "assets/transferofwaypoints/lang/uk.json");
           // api.RegisterTranslation("ru", "assets/transferofwaypoints/lang/ru.json");

            api.ChatCommands
                .Create("import")
                .WithDescription("Зчитує і загружає")
                .HandleWith(importWp);

            api.ChatCommands
                .Create("export")
                .WithDescription("Зчитує і вигружає.")
                .HandleWith(exportWp);
        }

        // Перша команда - записуємо точки з client-chat.log в filtered_waypoints.txt
        private TextCommandResult exportWp(TextCommandCallingArgs args)
        {
           

            if (isImporting)
            {
                return TextCommandResult.Success("Експорт вже активний.");
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
                    string startWord = "точки:"; // Початкове слово
                    string endSymbol = "@"; // Кінцевий символ

                    while ((line = reader.ReadLine()) != null)
                    {
                        // Початок блоку
                        if (line.Contains(startWord))
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

                return TextCommandResult.Success("Фільтровані дані збережені у файл: " + outputPath);
                
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
        }

        // Друга команда - імпортуємо точки з filtered_waypoints.txt і відправляємо їх в чат
        private TextCommandResult importWp(TextCommandCallingArgs args)
        {
          if (isImporting)
           {
                return TextCommandResult.Success("Імпорт  вже активний.");
           }

            isImporting = true;
            currentIndex = 0;  // Скидаємо індекс

            // Завантажуємо точки з файлу
            LoadWaypointsFromFile(outputPath);

            // Починаємо відправку точок
            api.World.RegisterGameTickListener(SendWaypoint, 1050);  // Відправляти кожну секунду (1000 мс)

            return TextCommandResult.Success("Імпорт точок почався.");
        }

        // Завантажуємо точки з файлу
        private void LoadWaypointsFromFile(string outputPath)
        {
            filteredLines.Clear();  // Очищаємо список попередніх точок

            using (StreamReader reader = new StreamReader(outputPath))
            {
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
                 api.ShowChatMessage("Всі точки імпортовані.");
            }
        }
    }
}

