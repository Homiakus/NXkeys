# Архитектура

```text
MenuScript
  → Command DLL
    → CommandHost
      → Configurator.exe (опционально)
      → DrawingEngine
        → ProfileValidator
        → DrawingPlanner
        → NxExecutionAdapter
          → NxLayerService
          → NxStyleService
          → NxAttributeService
          → NxFlatPatternService
          → NxSheetService
          → NxTitleBlockService
          → NxViewService
          → NxPmiService
          → NxAnnotationLayoutService
          → NxPartsListService
          → NxTechnicalRequirementsService
          → NxTableService
          → NxValidationService
          → NxExportService
```

## Границы модулей

`NxEskd.Core` не ссылается на NX Open. Его можно собирать и тестировать отдельно.

`NxEskd.NxRuntime` содержит только адаптацию плана к NX. Бизнес-правила не должны переноситься в этот проект.

`NxEskd.Configurator` редактирует JSON, но не имеет доступа к NX Session. Команда возвращается в NX через атомарный request-файл.

## Идемпотентность

Управляемые объекты помечаются:

```text
AUTO_DWG_MANAGED=true
AUTO_DWG_ID=<logical id>
AUTO_DWG_PROFILE_ID=<profile id>
AUTO_DWG_CONFIG_HASH=<sha256>
AUTO_DWG_GENERATOR_VERSION=<version>
```

Поиск выполняется сначала по `AUTO_DWG_ID`, затем по устойчивому имени. NX Tag в JSON не сохраняется.
