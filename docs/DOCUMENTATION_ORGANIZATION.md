# Documentation Organization Guide

## Recommended Structure

```
docs/
??? README.md                           # Main documentation index
??? architecture/                       # Architecture decisions
?   ??? ARCHITECTURE.md
?   ??? ARCHITECTURAL_REFINEMENT_ORCHESTRATION.md
?   ??? ARCHITECTURAL_REFINEMENT_CATCHALL.md
?   ??? ARCHITECTURAL_CLEANUP_FINAL.md
?   ??? ARCHITECTURAL_FINAL_POLISH.md
?   ??? ARCHITECTURE_CLEANUP.md
??? guides/                            # Development guides
?   ??? PLUGIN_DEVELOPMENT_GUIDE.md
?   ??? MULTI_STEP_PLUGIN_GUIDE.md
?   ??? YAML_CONFIGURATION_GUIDE.md
??? decisions/                         # Technical decisions
?   ??? REDIS_DECISION.md
?   ??? SINGLE_STEP_DECISION_PATTERN.md
?   ??? CONTEXT_SPECIFIC_PLUGINS.md
?   ??? YAML_NAMING_FIX.md
??? features/                          # Feature documentation
?   ??? ITEM_LEVEL_POLLING.md
?   ??? ITEM_LEVEL_ARCHITECTURE_UPDATE.md
?   ??? MULTI_STEP_ARCHITECTURE.md
?   ??? PARALLEL_EXECUTION_OPTIMIZATION.md
??? legacy/                            # Historical documentation
    ??? PHASE_1.5_YAML_LOADING.md
    ??? PHASE_1_BUILD_SUMMARY.md

Root level (move to docs/):
??? testing/
?   ??? E2E_TEST_REPORT.md
?   ??? TESTING_GUIDE_CLEAN.md
??? migration/
?   ??? MIGRATION_COMPLETE.md
?   ??? MIGRATION_STATUS.md
??? phases/
    ??? PHASE_0_COMPLETE.md
    ??? PHASE_1_COMPLETE.md
    ??? PHASE_1_GUIDE.md
    ??? PHASE_1_TESTING.md
```

## Manual Steps Required

1. **Create subdirectories:**
   ```powershell
   cd docs
   mkdir architecture, guides, decisions, features, legacy, testing, migration, phases
   ```

2. **Move root-level docs:**
   ```powershell
   # Testing docs
   mv ../E2E_TEST_REPORT.md testing/
   mv ../TESTING_GUIDE_CLEAN.md testing/
   
   # Migration docs
   mv ../MIGRATION_COMPLETE.md migration/
   mv ../MIGRATION_STATUS.md migration/
   
   # Phase docs
   mv ../PHASE_0_COMPLETE.md phases/
   mv ../PHASE_1_COMPLETE.md phases/
   mv ../PHASE_1_GUIDE.md phases/
   mv ../PHASE_1_TESTING.md phases/
   ```

3. **Organize existing docs:**
   ```powershell
   # Architecture
   mv ARCHITECTURAL_*.md architecture/
   mv ARCHITECTURE*.md architecture/
   
   # Guides
   mv PLUGIN_DEVELOPMENT_GUIDE.md guides/
   mv MULTI_STEP_PLUGIN_GUIDE.md guides/
   mv YAML_CONFIGURATION_GUIDE.md guides/
   
   # Decisions
   mv REDIS_DECISION.md decisions/
   mv SINGLE_STEP_DECISION_PATTERN.md decisions/
   mv CONTEXT_SPECIFIC_PLUGINS.md decisions/
   mv YAML_NAMING_FIX.md decisions/
   
   # Features
   mv ITEM_LEVEL_*.md features/
   mv MULTI_STEP_ARCHITECTURE.md features/
   mv PARALLEL_EXECUTION_OPTIMIZATION.md features/
   
   # Legacy
   mv PHASE_1*.md legacy/
   ```

4. **Create index:**
   ```powershell
   # Create docs/README.md with navigation
   ```

## Current Files to Organize

**Root Level (should move):**
- E2E_TEST_REPORT.md ? docs/testing/
- MIGRATION_COMPLETE.md ? docs/migration/
- MIGRATION_STATUS.md ? docs/migration/
- PHASE_0_COMPLETE.md ? docs/phases/
- PHASE_1_COMPLETE.md ? docs/phases/
- PHASE_1_GUIDE.md ? docs/phases/
- PHASE_1_TESTING.md ? docs/phases/
- TESTING_GUIDE_CLEAN.md ? docs/testing/

**docs/ (already in place, organize into subdirectories):**
- Architecture docs ? architecture/
- Plugin guides ? guides/
- Decision docs ? decisions/
- Feature docs ? features/
- Phase docs ? legacy/

## Benefits

? **Organized** - Easy to find relevant documentation
? **Categorized** - Clear purpose for each document
? **Maintainable** - Easy to add new docs
? **Professional** - Standard documentation structure
