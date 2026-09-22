# Valkyrie
### Save Editor for Valheim

[![Download for Windows x64](https://img.shields.io/badge/Download-Windows_x64-2ea44f?style=for-the-badge&logo=windows)](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases/download/v1.0.0/Editor.exe)
[![Скачать для Windows x64](https://img.shields.io/badge/Скачать-Windows_x64-2ea44f?style=for-the-badge)](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases/download/v1.0.0/Editor.exe)

**One file, no installation / Один файл, без установки.**
[Release notes / Описание релиза](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases/tag/v1.0.0) · [Nexus Mods](https://www.nexusmods.com/valheim/mods/3821)

The Nexus file is currently marked as suspicious pending a manual review. Until that review is complete, download from GitHub Releases. Do not disable antivirus protection.

[![Nexus Mods](https://img.shields.io/badge/Nexus_Mods-Valkyrie-orange?style=for-the-badge)](https://www.nexusmods.com/valheim/mods/3821)
[![Support the developer](https://img.shields.io/badge/Support%20the%20developer-DonationAlerts-orange?style=for-the-badge)](https://www.donationalerts.com/r/xoz9iuh)

[English](#english) | [Русский](#русский)

## English

**Valkyrie** is an unofficial Windows desktop editor for local Valheim character saves. Adjust skills, manage inventory and restore backups through a Russian or English interface with light and dark themes.

### Features

- Edit character names and existing skill levels using sliders or precise numeric input.
- Manage an inventory grid that follows the save (vanilla 8×4, expanded up to 8 rows).
- Add, replace or remove items with catalog stack limits and three quality modes: catalog, up to 99, up to 999.
- See item stats while picking: description, weight, food values, and leveled damage/armor.
- Copy and paste inventory slots with **Ctrl+C** / **Ctrl+V**. Click selects a slot; double-click opens it.
- Drag items to empty slots or swap items between occupied slots.
- Hide internal item variants by default, with an option to show them.
- Undo edits with **Ctrl+Z** or the Undo button.
- Clear inventory item cheat flags. Newly added items have this flag cleared.
- Scan local world `.chunk` files and clear cheated-item flags in containers, with backups.
- Save directly to the local character folder with automatic backups.
- Restore a backup after checking its format, checksum and character ID.

### Requirements

- Windows x64 for the standalone build. No .NET installation or internet connection is required to run it. Framework-dependent builds still require **.NET 8 Desktop Runtime**.
- **.NET 8 SDK** if building from source.
- Supported save formats: **profile 46, player 33, inventory 109, skills 2**. Other formats are rejected. Compatibility with every Valheim update is not guaranteed.
- Local character saves. Direct Steam Cloud editing is not supported.

### Getting Started

1. Close Valheim before editing and keep an independent backup of your character.
2. If a compiled version is available under [Releases](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases), extract the **entire archive**. Otherwise, build from source using the commands below.
3. Launch `Editor.exe`. The standalone version is one file: .NET, the catalog and icons are included. On first launch, native runtime components may be extracted automatically into the user's temporary directory.
4. Click **Open character** and select a `.fch` file.
5. Use **Skills** to change existing skill levels. On **Inventory**, click a slot to select it, double-click to edit, or drag items. Use **World** to scan local worlds for cheated container items.
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
dotnet publish Editor/Editor.csproj -p:PublishProfile=Standalone -o artifacts/standalone
```

For the standalone build, distribute only `artifacts/standalone/Editor.exe`. For framework-dependent builds, distribute the entire published folder. Catalog data and icons are embedded in both. Build output and private saves are excluded from Git. Runtime extraction does not require administrator privileges; the temporary directory must be writable.

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

- Automated round-trip and editing tests pass. The project owner has also reported successful in-game testing of this build; this does not guarantee compatibility with other game versions.
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
- Инвентарь по размеру сейва: обычный 8×4, расширенный до 8 строк.
- Добавление, замена и удаление предметов с лимитом стака из каталога и тремя режимами качества: каталог, до 99, до 999.
- Характеристики предмета при выборе: описание, вес, еда, урон и броня с бонусом уровня.
- Копирование и вставка ячеек через **Ctrl+C** / **Ctrl+V**. Клик выбирает ячейку, двойной клик открывает её.
- Перетаскивание в пустую ячейку или обмен предметами между занятыми ячейками.
- Скрытие служебных вариантов предметов с возможностью включить их отображение.
- Отмена правок через **Ctrl+Z** или кнопку отмены.
- Очистка чит-флагов предметов инвентаря. Новые предметы добавляются без этого флага.
- Анализ локальных файлов мира `.chunk` и снятие чит-флагов в контейнерах с резервными копиями.
- Сохранение прямо в папку персонажей с автоматическим резервированием.
- Восстановление копий с проверкой формата, контрольной суммы и ID персонажа.

### Требования

- Windows x64 для автономной сборки. Устанавливать .NET и подключаться к интернету для запуска не нужно. Для обычной сборки по-прежнему нужен **.NET 8 Desktop Runtime**.
- **.NET 8 SDK**, если собираете программу из исходников.
- Поддерживаемые форматы сейва: **профиль 46, персонаж 33, инвентарь 109, навыки 2**. Другие форматы отклоняются. Совместимость с каждым обновлением Valheim не гарантируется.
- Локальные сохранения. Прямое редактирование Steam Cloud не поддерживается.

### Как Пользоваться

1. Закройте Valheim и сделайте отдельную резервную копию персонажа.
2. Если готовая сборка опубликована в [Releases](https://github.com/Xoz9IuH2/Valkyrie---Save-Editor-for-Valheim/releases), распакуйте **весь архив**. Иначе соберите программу по инструкции ниже.
3. Запустите `Editor.exe`. Автономная версия состоит из одного файла: .NET, каталог и иконки уже встроены. При первом запуске нативные компоненты среды выполнения могут автоматически распаковаться во временную папку пользователя.
4. Нажмите **«Открыть персонажа»** и выберите файл `.fch`.
5. Меняйте уровни на вкладке **«Навыки»**. На вкладке **«Инвентарь»** клик выбирает ячейку, двойной клик открывает предмет. Вкладка **«Мир»** ищет чит-флаги в контейнерах локального мира.
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
dotnet publish Editor/Editor.csproj -p:PublishProfile=Standalone -o artifacts/standalone
```

Для автономной версии достаточно передать `artifacts/standalone/Editor.exe`. Для обычной сборки передавайте всю папку публикации. Каталог и иконки встроены в обе версии. Права администратора для распаковки не нужны, но временная папка должна быть доступна для записи. Готовые сборки и личные сейвы исключены из Git.

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

- Автоматические проверки чтения, записи и редактирования проходят. Владелец проекта также подтвердил успешную проверку этой сборки в игре; это не гарантирует совместимость с другими версиями Valheim.
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
