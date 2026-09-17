# Valkyrie
### Save Editor for Valheim

[![Support the developer](https://img.shields.io/badge/Support%20the%20developer-DonationAlerts-orange?style=for-the-badge)](https://www.donationalerts.com/r/xoz9iuh)

[English](#english) | [Русский](#русский)

## English

**Valkyrie** is an unofficial Windows desktop editor for local Valheim character saves. Adjust skills, manage inventory and restore backups through a Russian or English interface with light and dark themes.

### Features

- Edit character names and existing skill levels using sliders or precise numeric input.
- Manage an 8×4 inventory with item icons, search and category filters.
- Add, replace or remove items with per-item stack and quality limits.
- Drag items to empty slots or swap items between occupied slots.
- Hide internal item variants by default, with an option to show them.
- Undo edits with **Ctrl+Z** or the Undo button.
- Clear inventory item cheat flags. Newly added items have this flag cleared.
- Save directly to the local character folder with automatic backups.
- Restore a backup after checking its format, checksum and character ID.

### Requirements

- Windows with **.NET 8 Desktop Runtime** for the framework-dependent build.
- **.NET 8 SDK** if building from source.
- Supported save formats: **profile 46, player 33, inventory 109, skills 2**. Other formats are rejected. Compatibility with every Valheim update is not guaranteed.
- Local character saves. Direct Steam Cloud editing is not supported.

### Getting Started

1. Close Valheim before editing and keep an independent backup of your character.
2. If a compiled version is available under [Releases](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases), extract the **entire archive**. Otherwise, build from source using the commands below.
3. Launch `Editor.exe` from the published folder. Keep `items.json`, `Icons` and the other published files beside it.
4. Click **Open character** and select a `.fch` file.
5. Use **Skills** to change existing skill levels. Use **Inventory** to select, add, edit, move or remove items.
6. Click **Save to game** and confirm the replacement. The game must be closed.

The default local save folder is resolved for the current Windows user:

```text
%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\characters_local
```

For a cloud character, use Valheim's save-management interface to move the character to local storage first.

### Backups And Recovery

Before replacing an existing save, Valkyrie retains the previous file in `characters_local\Backups`. A file changed externally since opening will not be overwritten.

To restore, open the local character, click **Restore backup**, select the backup and confirm. The current on-disk save is backed up before replacement. A backup belonging to a different character is rejected.

Undo affects unsaved edits only. Opening, saving or restoring clears the undo history.

### Build From Source

Run from the repository root:

```powershell
dotnet build Editor/Editor.csproj -c Release
dotnet publish Editor/Editor.csproj -c Release -o ValheimEditor
./ValheimEditor/Editor.exe
```

To create a standalone Windows x64 build with the .NET runtime included:

```powershell
dotnet publish Editor/Editor.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Distribute the entire published folder, not just the executable. Build output and private saves are excluded from Git.

### Testing And Catalog Updates

Tests read a supported character file without modifying it. Storage tests use temporary files:

```powershell
dotnet Editor/bin/Release/net8.0-windows/Editor.dll --test "C:\path\character.fch"
```

Exit code `0` means success. Results are written to `test-results.txt` beside the executable.

Item names, icons and limits are extracted from game assets and do not update automatically. Python is needed only to regenerate the catalog:

```powershell
python -m pip install -r requirements-extract.txt
python Editor/extract_catalog.py "C:\path\to\Valheim" Editor/items.json
```

The extractor currently expects bundles `c4210710`, `6a33a62` and `resources.assets`. Game updates may require changes to both the extractor and save parser.

### Limitations

- Automated round-trip and editing tests pass; in-game loading of edited saves has not yet been verified by the developer.
- Internal-item filtering uses heuristics. Check the technical item ID before adding unusual variants.
- Clearing item flags does not clear profile-level cheat history or guarantee achievement eligibility.
- Adding new skills and editing maps are not implemented.
- Keep independent backups. Do not upload private character files to public issues or repositories.

### Support Development

If you find Valkyrie useful, you can support development through [DonationAlerts](https://www.donationalerts.com/r/xoz9iuh). Support is entirely optional.

For bug reports, use [GitHub Issues](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/issues). Include your game version, steps to reproduce and the error message; avoid publishing your save file.

---

## Русский

**Valkyrie** — неофициальный редактор локальных сохранений персонажей Valheim для Windows. Позволяет изменять навыки, управлять инвентарём и восстанавливать резервные копии. Доступны русский и английский интерфейсы, светлая и тёмная темы.

### Возможности

- Изменение имени персонажа и имеющихся навыков ползунком или точным числом.
- Инвентарь 8×4 с иконками, поиском и фильтрами категорий.
- Добавление, замена и удаление предметов с ограничениями размера стака и уровня.
- Перетаскивание в пустую ячейку или обмен предметами между занятыми ячейками.
- Скрытие служебных вариантов предметов с возможностью включить их отображение.
- Отмена правок через **Ctrl+Z** или кнопку отмены.
- Очистка чит-флагов предметов инвентаря. Новые предметы добавляются без этого флага.
- Сохранение прямо в папку персонажей с автоматическим резервированием.
- Восстановление копий с проверкой формата, контрольной суммы и ID персонажа.

### Требования

- Windows и **.NET 8 Desktop Runtime** для обычной сборки.
- **.NET 8 SDK**, если собираете программу из исходников.
- Поддерживаемые форматы сейва: **профиль 46, персонаж 33, инвентарь 109, навыки 2**. Другие форматы отклоняются. Совместимость с каждым обновлением Valheim не гарантируется.
- Локальные сохранения. Прямое редактирование Steam Cloud не поддерживается.

### Как Пользоваться

1. Закройте Valheim и сделайте отдельную резервную копию персонажа.
2. Если готовая сборка опубликована в [Releases](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases), распакуйте **весь архив**. Иначе соберите программу по инструкции ниже.
3. Запустите `Editor.exe`. Рядом должны оставаться `items.json`, папка `Icons` и остальные файлы сборки.
4. Нажмите **«Открыть персонажа»** и выберите файл `.fch`.
5. Меняйте уровни на вкладке **«Навыки»**. На вкладке **«Инвентарь»** нажмите на ячейку для выбора предмета или перетащите предмет в другую ячейку.
6. Нажмите **«Сохранить в игру»** и подтвердите замену. Игра должна быть закрыта.

Папка локальных персонажей определяется для текущего пользователя Windows:

```text
%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\characters_local
```

Если персонаж хранится в облаке, сначала перенесите его в локальное хранилище через управление сохранениями в Valheim.

### Резервные Копии

Перед заменой существующего сейва предыдущий файл сохраняется в `characters_local\Backups`. Если файл изменился после открытия в редакторе, программа откажется его перезаписывать.

Для восстановления откройте локального персонажа, нажмите **«Восстановить копию»**, выберите файл и подтвердите действие. Текущий сейв тоже будет зарезервирован. Копию другого персонажа восстановить поверх открытого нельзя.

Отмена работает только для несохранённых правок. Открытие, сохранение и восстановление очищают историю отмены.

### Сборка Из Исходников

Выполните из корня репозитория:

```powershell
dotnet build Editor/Editor.csproj -c Release
dotnet publish Editor/Editor.csproj -c Release -o ValheimEditor
./ValheimEditor/Editor.exe
```

Автономная сборка Windows x64 со встроенным .NET:

```powershell
dotnet publish Editor/Editor.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Передавать нужно всю папку сборки, а не только `.exe`. Готовые сборки и личные сейвы исключены из Git.

### Проверки И Обновление Каталога

Тесты читают сейв поддерживаемой версии, не изменяя оригинал. Проверки записи выполняются на временных файлах:

```powershell
dotnet Editor/bin/Release/net8.0-windows/Editor.dll --test "C:\path\character.fch"
```

Код завершения `0` означает успех. Отчёт находится в `test-results.txt` рядом с исполняемым файлом.

Названия, иконки и ограничения предметов извлечены из ресурсов игры. Каталог не обновляется автоматически. Python нужен только для повторного извлечения:

```powershell
python -m pip install -r requirements-extract.txt
python Editor/extract_catalog.py "C:\path\to\Valheim" Editor/items.json
```

Экстрактор рассчитан на пакеты `c4210710`, `6a33a62` и `resources.assets`. После обновления игры может потребоваться изменение экстрактора и обработчика сохранений.

### Ограничения

- Автоматические проверки чтения, записи и редактирования проходят; загрузка изменённых сейвов в самой игре разработчиком пока не подтверждена.
- Определение служебных предметов приблизительное. Проверяйте технический ID необычных вариантов.
- Очистка флагов предметов не очищает историю читов персонажа и не гарантирует доступность достижений.
- Добавление новых навыков и редактирование карты пока не реализованы.
- Храните независимые резервные копии. Не публикуйте личные сейвы в открытых задачах или репозиториях.

### Поддержка Разработчика

[![Поддержать разработчика](https://img.shields.io/badge/Поддержать_разработчика-DonationAlerts-orange?style=for-the-badge)](https://www.donationalerts.com/r/xoz9iuh)

Если Valkyrie вам полезна, поддержать разработку можно через [DonationAlerts](https://www.donationalerts.com/r/xoz9iuh). Поддержка полностью добровольная.

Об ошибках сообщайте через [GitHub Issues](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/issues): укажите версию игры, шаги воспроизведения и текст ошибки. Личный сейв публично прикладывать не нужно.

---

> [!IMPORTANT]
> Данный проект является неофициальным фанатским инструментом. Все графические ресурсы, иконки и торговые марки Valheim принадлежат Iron Gate Studio и Coffee Stain Publishing. Автор не связан с разработчиками игры.
