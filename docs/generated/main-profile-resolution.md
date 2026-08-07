# NX 2512 main profile resolution

- Source intents: **1169**
- Selected frequencies: **K3, K4, K5**
- Selected unique intents: **885**
- Runtime support commands: **284**
- Generated module rows: **2354**
- Enabled rows: **691**
- Existing rows: **364**
- Resolved rows: **327**
- Ambiguous rows: **45**
- Unresolved rows: **1618**

> Disabled ambiguous/unresolved rows keep their mnemonic path but cannot dispatch a fabricated BUTTON ID.

## Disabled commands

| Command | Module | Frequency | Status | Candidates |
|---|---|---|---|---|
| Close | modeling | K3 | unresolved | — |
| Close All | modeling | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | modeling | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | modeling | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | modeling | K3 | unresolved | — |
| Print | modeling | K3 | unresolved | — |
| Export | modeling | K4 | unresolved | — |
| Import | modeling | K4 | unresolved | — |
| Recently Opened Parts | modeling | K3 | unresolved | — |
| Switch Window | modeling | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | modeling | K3 | unresolved | — |
| Rename | modeling | K3 | unresolved | — |
| Object Properties | modeling | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | modeling | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | modeling | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | modeling | K3 | unresolved | — |
| Unsuppress | modeling | K3 | unresolved | — |
| Reorder | modeling | K3 | unresolved | — |
| Make Current Feature | modeling | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | modeling | K3 | unresolved | — |
| Selection Filter | modeling | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | modeling | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | modeling | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | modeling | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | modeling | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | modeling | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | modeling | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | modeling | K5 | unresolved | — |
| Pan | modeling | K5 | unresolved | — |
| Rotate | modeling | K5 | unresolved | — |
| Orient View | modeling | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | modeling | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | modeling | K3 | unresolved | — |
| Back | modeling | K3 | unresolved | — |
| Top | modeling | K3 | unresolved | — |
| Bottom | modeling | K3 | unresolved | — |
| Left | modeling | K3 | unresolved | — |
| Right | modeling | K3 | unresolved | — |
| Previous View | modeling | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | modeling | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | modeling | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | modeling | K3 | unresolved | — |
| Hide | modeling | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | modeling | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | modeling | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | modeling | K3 | unresolved | — |
| Shaded | modeling | K3 | unresolved | — |
| Shaded with Edges | modeling | K3 | unresolved | — |
| Examine Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | modeling | K3 | unresolved | — |
| Customize | modeling | K3 | unresolved | — |
| Roles | modeling | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | modeling | K3 | unresolved | — |
| Part Navigator | modeling | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Sweep Along Guide | modeling | K3 | unresolved | — |
| Tube | modeling | K3 | unresolved | — |
| Block | modeling | K3 | unresolved | — |
| Cylinder | modeling | K3 | unresolved | — |
| Cone | modeling | K3 | unresolved | — |
| Sphere | modeling | K3 | unresolved | — |
| Thread | modeling | K3 | unresolved | — |
| Boss | modeling | K3 | unresolved | — |
| Pocket | modeling | K3 | unresolved | — |
| Pad | modeling | K3 | unresolved | — |
| Slot | modeling | K3 | unresolved | — |
| Groove | modeling | K3 | unresolved | — |
| Rib | modeling | K3 | unresolved | — |
| Dart | modeling | K3 | unresolved | — |
| Emboss | modeling | K3 | unresolved | — |
| Unite | modeling | K5 | unresolved | — |
| Subtract | modeling | K5 | unresolved | — |
| Intersect | modeling | K3 | unresolved | — |
| Split Body | modeling | K4 | unresolved | UG_SEL_BODY_PRIORITY (0.25) |
| Unsew | modeling | K3 | unresolved | UG_MODELING_SEW_FEATURE (0.29)<br>UG_FILE_NEW (0.25) |
| Patch | modeling | K3 | unresolved | — |
| Create Body | modeling | K4 | unresolved | UG_CAM_CREATE_TOOL (0.50)<br>UG_SIM_CREATE_LOAD (0.50)<br>UG_ROUTE_CREATE_ROUTE (0.47) |
| Promote Body | modeling | K4 | unresolved | — |
| Clone Body | modeling | K4 | unresolved | — |
| Face Blend | modeling | K3 | unresolved | UG_MODELING_BLEND_FEATURE (0.52)<br>UG_ANALYSIS_FACE_CURVATURE (0.38)<br>UG_SEL_FACE_PRIORITY (0.29) |
| Draft | modeling | K3 | unresolved | UG_APP_DRAFTING (0.26) |
| Shell | modeling | K3 | unresolved | — |
| Thicken | modeling | K3 | unresolved | — |
| Offset Face | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.47)<br>UG_ANALYSIS_FACE_CURVATURE (0.32)<br>UG_SEL_FACE_PRIORITY (0.26) |
| Offset Surface | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.49)<br>UG_MODELING_STUDIO_SURFACE_FEATURE (0.47)<br>UG_APP_MODELING (0.40) |
| Resize Blend | modeling | K3 | unresolved | UG_MODELING_BLEND_FEATURE (0.51) |
| Replace Face | modeling | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.42)<br>UG_REPLACE_FEATURE_TEMPLATE (0.37)<br>UG_ANALYSIS_FACE_CURVATURE (0.32) |
| Move Face | modeling | K4 | unresolved | UG_ASSEMBLIES_MOVE_COMPONENT (0.37)<br>UG_LAYER_MOVE (0.37)<br>UG_ANALYSIS_FACE_CURVATURE (0.35) |
| Pull Face | modeling | K3 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.32) |
| Divide Face | modeling | K3 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.29) |
| Simplify Body | modeling | K4 | unresolved | — |
| Defeature | modeling | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.29)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.26) |
| Linear Pattern | modeling | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.47)<br>UG_SHEET_METAL_FLAT_PATTERN (0.46)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.34) |
| Circular Pattern | modeling | K4 | unresolved | UG_SHEET_METAL_FLAT_PATTERN (0.46)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.31) |
| Pattern Face | modeling | K4 | unresolved | UG_MODELING_PATTERNFEATURE_FEATURE (0.54)<br>UG_ASSEMBLIES_PATTERN_COMPONENT (0.42)<br>UG_ANALYSIS_FACE_CURVATURE (0.32) |
| Pattern Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.49)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.49)<br>UG_ASSEMBLIES_PATTERN_COMPONENT (0.42) |
| Mirror Body | modeling | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.44) |
| Move Object | modeling | K5 | unresolved | UG_ASSEMBLIES_MOVE_COMPONENT (0.43)<br>UG_INFO_OBJECT (0.35)<br>UG_ROUTE_DELETE (0.34) |
| Move Feature | modeling | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.50)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.48) |
| Transform Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_MODELING_WAVE_LINKER (0.29) |
| Scale Body | modeling | K4 | unresolved | — |
| Instance Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.50)<br>UG_MODELING_WAVE_LINKER (0.33)<br>UG_ASSY_WAVE_LINKER (0.29) |
| Datum Plane | modeling | K5 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_SEL_DATUM_PRIORITY (0.29) |
| Datum Axis | modeling | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_SEL_DATUM_PRIORITY (0.27) |
| Datum CSYS | modeling | K5 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_SEL_DATUM_PRIORITY (0.27) |
| Point | modeling | K3 | unresolved | — |
| Point Set | modeling | K3 | unresolved | — |
| Coordinate System | modeling | K3 | unresolved | — |
| WCS | modeling | K3 | unresolved | — |
| Orient WCS | modeling | K3 | unresolved | — |
| Move WCS | modeling | K4 | unresolved | UG_ASSEMBLIES_MOVE_COMPONENT (0.34)<br>UG_LAYER_MOVE (0.31) |
| Datum on Path | modeling | K4 | unresolved | UG_SEL_DATUM_PRIORITY (0.31)<br>UG_CAM_GENERATE_TOOL_PATH (0.30)<br>UG_CAM_VERIFY_TOOL_PATH (0.30) |
| Arc/Circle | modeling | K3 | ambiguous | UG_SKETCH_CIRCLE (0.90)<br>UG_SKETCH_ARC (0.84)<br>UG_SKETCH_ARC_FROM_CENTER (0.31) |
| Studio Spline | modeling | K3 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.50) |
| Bridge Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.40)<br>UG_MODELING_THROUGH_CURVES_FEATURE (0.28) |
| Helix | modeling | K3 | unresolved | — |
| Law Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.40) |
| Curve on Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.42)<br>UG_APP_MODELING (0.37)<br>UG_SKETCH_OFFSET_CURVE (0.28) |
| Projected Curve | modeling | K4 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.47)<br>UG_SKETCH_OFFSET_CURVE (0.42) |
| Intersection Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.40)<br>UG_SEL_CURVE_PRIORITY (0.32) |
| Section Curve | modeling | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.45)<br>UG_SKETCH_OFFSET_CURVE (0.39)<br>UG_SEL_CURVE_PRIORITY (0.30) |
| Offset in Face | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.39)<br>UG_ANALYSIS_FACE_CURVATURE (0.27) |
| Join Curves | modeling | K4 | unresolved | UG_MODELING_THROUGH_CURVES_FEATURE (0.47) |
| Divide Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.40) |
| Simplify Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.37) |
| Composite Curve | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.45) |
| Extract Curve | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_SKETCH_OFFSET_CURVE (0.42) |
| N-Sided Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.43)<br>UG_APP_MODELING (0.35) |
| Bridge Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.50)<br>UG_APP_MODELING (0.40)<br>UG_PMI_SURFACE_FINISH (0.28) |
| Ruled Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.50)<br>UG_APP_MODELING (0.38)<br>UG_PMI_SURFACE_FINISH (0.28) |
| Bounded Plane | modeling | K3 | unresolved | — |
| Fill Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.47)<br>UG_APP_MODELING (0.40)<br>UG_PMI_SURFACE_FINISH (0.28) |
| Trimmed Sheet | modeling | K3 | unresolved | UG_MODELING_TRIM_SHEET_FEATURE (0.55)<br>UG_MODELING_FF_EXTEND_SHEET (0.45)<br>UG_MODELING_SHEET_FEATURE (0.29) |
| Variable Offset | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.30) |
| Face Analysis | modeling | K4 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.44)<br>UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.36)<br>UG_SEL_FACE_PRIORITY (0.27) |
| Match Edge | modeling | K3 | unresolved | — |
| Match Surface | modeling | K4 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.47)<br>UG_APP_MODELING (0.45)<br>UG_PMI_SURFACE_FINISH (0.28) |
| Global Shaping | modeling | K3 | unresolved | — |
| X-Form | modeling | K3 | unresolved | — |
| I-Form | modeling | K3 | unresolved | — |
| Flattening and Forming | modeling | K4 | unresolved | — |
| Offset Region | modeling | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.43) |
| Resize Face | modeling | K3 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.30)<br>UG_ANALYSIS_FACE_CURVATURE (0.29)<br>UG_SKETCH_RAPID_DIMENSION (0.29) |
| Make Coplanar | modeling | K3 | unresolved | — |
| Make Collinear | modeling | K3 | unresolved | — |
| Make Symmetric | modeling | K3 | unresolved | — |
| Recognize Feature | modeling | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.43)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.43)<br>UG_MODELING_SHEET_FEATURE (0.43) |
| Optimize Face | modeling | K4 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.26) |
| Local Scale | modeling | K3 | unresolved | — |
| Part Families | modeling | K3 | unresolved | UG_NAVIGATOR_PART (0.40)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.32)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.30) |
| Feature Group | modeling | K4 | unresolved | UG_PMI_FEATURE_CONTROL_FRAME (0.38)<br>UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_SEL_FEATURE_PRIORITY (0.34) |
| Feature Browser | modeling | K4 | unresolved | UG_PMI_FEATURE_CONTROL_FRAME (0.40)<br>UG_ASSY_WAVE_GRAPH_BROWSER (0.35)<br>UG_SEL_FEATURE_PRIORITY (0.34) |
| Feature Playback | modeling | K4 | unresolved | UG_PMI_FEATURE_CONTROL_FRAME (0.38)<br>UG_CREATE_FEATURE_TEMPLATE (0.34)<br>UG_REPLACE_FEATURE_TEMPLATE (0.34) |
| Edit Dependency | modeling | K4 | unresolved | UG_PMI_EDIT (0.36)<br>UG_ROUTE_EDIT_ROUTE (0.36)<br>UG_EDIT_DELETE (0.31) |
| Delay Update | modeling | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.26) |
| Update Model | modeling | K5 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_PMI_MODEL_VIEW (0.33)<br>UG_APP_MODELING (0.27) |
| Timestamp Order | modeling | K3 | unresolved | — |
| User Defined Feature | modeling | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.41)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.39)<br>UG_MODELING_SEW_FEATURE (0.39) |
| Import STL | modeling | K4 | unresolved | — |
| Import OBJ | modeling | K4 | unresolved | — |
| Faceted Body | modeling | K3 | unresolved | — |
| Convert to Convergent Body | modeling | K3 | unresolved | — |
| Offset Facet Body | modeling | K3 | unresolved | UG_SKETCH_OFFSET_CURVE (0.34) |
| Split Convergent Body | modeling | K3 | unresolved | — |
| Wrap Geometry | modeling | K3 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.52)<br>UG_MODELING_WAVE_LINKER (0.39)<br>UG_ASSY_WAVE_LINKER (0.35) |
| Generate B-Rep | modeling | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.35) |
| Check In | modeling | K3 | unresolved | — |
| Check Out | modeling | K3 | unresolved | — |
| Cancel Check Out | modeling | K3 | unresolved | — |
| Impact Analysis | modeling | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | modeling | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | modeling | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | modeling | K3 | unresolved | — |
| Import Parasolid | modeling | K4 | unresolved | — |
| Import STEP | modeling | K4 | unresolved | — |
| Import IGES | modeling | K4 | unresolved | — |
| Import JT | modeling | K4 | unresolved | — |
| Import CATIA | modeling | K4 | unresolved | — |
| Import Creo | modeling | K4 | unresolved | — |
| Import SolidWorks | modeling | K4 | unresolved | — |
| Import DXF/DWG | modeling | K4 | unresolved | — |
| Import IFC | modeling | K4 | unresolved | — |
| Import XML | modeling | K4 | unresolved | — |
| Export Parasolid | modeling | K4 | unresolved | — |
| Export STEP AP203/214/242 | modeling | K4 | unresolved | — |
| Export IGES | modeling | K4 | unresolved | — |
| Export JT | modeling | K4 | unresolved | — |
| Export DXF/DWG | modeling | K4 | unresolved | — |
| Export STL | modeling | K4 | unresolved | — |
| Export 3MF | modeling | K4 | unresolved | — |
| Export PDF | modeling | K4 | unresolved | — |
| Export CGM | modeling | K4 | unresolved | — |
| Export QIF | modeling | K4 | unresolved | — |
| Publish Technical Data Package | modeling | K3 | unresolved | — |
| Heal Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | modeling | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | modeling | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | modeling | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | modeling | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| Export Command List | modeling | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | modeling | K3 | unresolved | — |
| Profile | sketch | K5 | unresolved | — |
| Ellipse | sketch | K3 | unresolved | — |
| Polygon | sketch | K3 | unresolved | UG_SKETCH_RECTANGLE (0.33) |
| Studio Spline | sketch | K3 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.46) |
| Conic | sketch | K3 | unresolved | — |
| Point | sketch | K3 | unresolved | — |
| Slot | sketch | K3 | unresolved | — |
| Pattern Curve | sketch | K4 | unresolved | UG_MODELING_PATTERNFEATURE_FEATURE (0.47)<br>UG_SKETCH_OFFSET_CURVE (0.45)<br>UG_ASSEMBLIES_PATTERN_COMPONENT (0.44) |
| Mirror Curve | sketch | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.46)<br>UG_SKETCH_OFFSET_CURVE (0.44) |
| Project Curve | sketch | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.49) |
| Intersection Curve | sketch | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.44)<br>UG_SEL_CURVE_PRIORITY (0.32) |
| Include Geometry | sketch | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.43)<br>UG_ASSY_WAVE_LINKER (0.29)<br>UG_MODELING_WAVE_LINKER (0.29) |
| Equation Curve | sketch | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.41)<br>UG_SEL_CURVE_PRIORITY (0.27)<br>UG_SKETCH_LINE_BY_TWO_POINTS (0.26) |
| Horizontal Dimension | sketch | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.50)<br>UG_SKETCH_HORIZONTAL_CONSTRAINT (0.47)<br>UG_SKETCH_RAPID_DIMENSION (0.46) |
| Vertical Dimension | sketch | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.48)<br>UG_SKETCH_RAPID_DIMENSION (0.48)<br>UG_DRAFTING_RAPID_DIMENSION (0.45) |
| Parallel Dimension | sketch | K4 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.51)<br>UG_DRAFTING_RAPID_DIMENSION (0.47)<br>UG_PMI_RAPID_DIMENSION (0.47) |
| Perpendicular Dimension | sketch | K4 | unresolved | UG_SKETCH_PERPENDICULAR_CONSTRAINT (0.49)<br>UG_SKETCH_LINEAR_DIMENSION (0.47)<br>UG_SKETCH_RAPID_DIMENSION (0.45) |
| Angular Dimension | sketch | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.52)<br>UG_SKETCH_RAPID_DIMENSION (0.48)<br>UG_DRAFTING_RAPID_DIMENSION (0.44) |
| Radial Dimension | sketch | K4 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.57)<br>UG_DRAFTING_RAPID_DIMENSION (0.53)<br>UG_PMI_RAPID_DIMENSION (0.53) |
| Diameter Dimension | sketch | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.53)<br>UG_SKETCH_RAPID_DIMENSION (0.48)<br>UG_DRAFTING_RAPID_DIMENSION (0.45) |
| Perimeter Dimension | sketch | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.52)<br>UG_SKETCH_RAPID_DIMENSION (0.47)<br>UG_DRAFTING_RAPID_DIMENSION (0.44) |
| Geometric Constraints | sketch | K5 | unresolved | UG_ASSEMBLIES_CONSTRAINTS (0.45)<br>UG_INFO_GEOMETRIC_MEASUREMENT (0.30)<br>UG_SKETCH_VERTICAL_CONSTRAINT (0.30) |
| Collinear | sketch | K3 | unresolved | — |
| Concentric | sketch | K3 | unresolved | — |
| Equal Length | sketch | K3 | unresolved | — |
| Equal Radius | sketch | K3 | unresolved | — |
| Symmetric | sketch | K3 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Fix | sketch | K3 | unresolved | UG_VIEW_FIT (0.28) |
| Make Reference | sketch | K3 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.48)<br>UG_SKETCH_RAPID_DIMENSION (0.45) |
| Convert to Driving | sketch | K3 | unresolved | UG_APP_DRAFTING (0.30)<br>UG_APP_ROUTING (0.28)<br>UG_LAYER_MOVE (0.28) |
| Corner | sketch | K3 | unresolved | — |
| Move Curve | sketch | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.47)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.40)<br>UG_LAYER_MOVE (0.34) |
| Drag | sketch | K3 | unresolved | — |
| Scale Curve | sketch | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.47)<br>UG_SEL_CURVE_PRIORITY (0.26) |
| Show/Remove Constraints | sketch | K4 | unresolved | UG_ASSEMBLIES_CONSTRAINTS (0.40)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.35)<br>UG_ROUTE_REMOVE_PART (0.31) |
| Alternate Solution | sketch | K3 | unresolved | UG_SIM_CREATE_SOLUTION (0.50) |
| Auto Constrain | sketch | K3 | unresolved | UG_SKETCH_TANGENT_CONSTRAINT (0.29)<br>UG_SKETCH_PARALLEL_CONSTRAINT (0.28)<br>UG_SKETCH_VERTICAL_CONSTRAINT (0.28) |
| Continuous Auto Dimensioning | sketch | K4 | unresolved | — |
| Close | assembly | K3 | unresolved | — |
| Close All | assembly | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | assembly | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | assembly | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | assembly | K3 | unresolved | — |
| Print | assembly | K3 | unresolved | — |
| Export | assembly | K4 | unresolved | — |
| Import | assembly | K4 | unresolved | — |
| Recently Opened Parts | assembly | K3 | unresolved | — |
| Switch Window | assembly | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | assembly | K3 | unresolved | — |
| Rename | assembly | K3 | unresolved | — |
| Object Properties | assembly | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | assembly | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | assembly | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | assembly | K3 | unresolved | — |
| Unsuppress | assembly | K3 | unresolved | — |
| Reorder | assembly | K3 | unresolved | — |
| Make Current Feature | assembly | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | assembly | K3 | unresolved | — |
| Selection Filter | assembly | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | assembly | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | assembly | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | assembly | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | assembly | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | assembly | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | assembly | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | assembly | K5 | unresolved | — |
| Pan | assembly | K5 | unresolved | — |
| Rotate | assembly | K5 | unresolved | — |
| Orient View | assembly | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | assembly | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | assembly | K3 | unresolved | — |
| Back | assembly | K3 | unresolved | — |
| Top | assembly | K3 | unresolved | — |
| Bottom | assembly | K3 | unresolved | — |
| Left | assembly | K3 | unresolved | — |
| Right | assembly | K3 | unresolved | — |
| Previous View | assembly | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | assembly | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | assembly | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | assembly | K3 | unresolved | — |
| Hide | assembly | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | assembly | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | assembly | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | assembly | K3 | unresolved | — |
| Shaded | assembly | K3 | unresolved | — |
| Shaded with Edges | assembly | K3 | unresolved | — |
| Examine Geometry | assembly | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | assembly | K3 | unresolved | — |
| Customize | assembly | K3 | unresolved | — |
| Roles | assembly | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | assembly | K3 | unresolved | — |
| Part Navigator | assembly | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Rename Component | assembly | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.57)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.57)<br>UG_ASSEMBLIES_ADD_COMPONENT (0.52) |
| Make Work Part | assembly | K5 | unresolved | UG_ROUTE_REMOVE_PART (0.35)<br>UG_ROUTE_PLACE_PART (0.32)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.31) |
| Make Displayed Part | assembly | K3 | unresolved | UG_ROUTE_PLACE_PART (0.34)<br>UG_ROUTE_REMOVE_PART (0.30)<br>UG_FILE_SAVE_PART (0.27) |
| Close Component | assembly | K4 | unresolved | UG_ASSEMBLIES_MOVE_COMPONENT (0.56)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.54)<br>UG_ASSEMBLIES_REPLACE_COMPONENT (0.52) |
| Promote Body | assembly | K4 | unresolved | — |
| Deformable Part | assembly | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.42)<br>UG_ROUTE_PLACE_PART (0.39)<br>UG_NAVIGATOR_PART (0.29) |
| Mirror Assembly | assembly | K4 | unresolved | UG_APP_ASSEMBLIES (0.41)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.39)<br>UG_ASSEMBLIES_NAVIGATOR (0.28) |
| Clone Assembly | assembly | K3 | unresolved | UG_APP_ASSEMBLIES (0.41)<br>UG_ASSEMBLIES_NAVIGATOR (0.28)<br>UG_ASSEMBLIES_CONSTRAINTS (0.27) |
| Pack and Go | assembly | K3 | unresolved | UG_APP_GATEWAY (0.30)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.28) |
| Touch/Align | assembly | K3 | unresolved | — |
| Concentric | assembly | K3 | unresolved | — |
| Distance | assembly | K3 | unresolved | — |
| Angle | assembly | K3 | unresolved | — |
| Center | assembly | K3 | ambiguous | UG_SKETCH_ARC_FROM_CENTER (0.86)<br>UG_SKETCH_CIRCLE_FROM_CENTER (0.85)<br>UG_SKETCH_RECTANGLE_FROM_CENTER (0.84) |
| Bond | assembly | K3 | unresolved | UG_SHEET_METAL_BEND (0.32) |
| Fix Component | assembly | K4 | unresolved | UG_ASSEMBLIES_ADD_COMPONENT (0.55)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.53)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.49) |
| Drag Component | assembly | K4 | unresolved | UG_ASSEMBLIES_ADD_COMPONENT (0.53)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.53)<br>UG_ASSEMBLIES_REPLACE_COMPONENT (0.50) |
| Remember Constraints | assembly | K4 | unresolved | UG_ASSEMBLIES_CONSTRAINTS (0.54)<br>UG_SIM_CREATE_CONSTRAINT (0.27)<br>UG_SKETCH_PARALLEL_CONSTRAINT (0.25) |
| Positioning Task | assembly | K3 | unresolved | — |
| Interpart Link | assembly | K3 | unresolved | UG_ASSY_WAVE_INTERFACE_LINKER (0.26) |
| Design in Context | assembly | K3 | unresolved | — |
| Create Interpart Expression | assembly | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.36)<br>UG_SIM_CREATE_SOLUTION (0.32)<br>UG_ASSEMBLIES_NEW_COMPONENT (0.31) |
| Edit Context | assembly | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.47)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_PASTE (0.31) |
| Product Interface | assembly | K3 | unresolved | UG_ASSY_WAVE_INTERFACE_LINKER (0.32) |
| Reference Set | assembly | K3 | unresolved | — |
| Create Reference Set | assembly | K4 | unresolved | UG_ASSEMBLIES_NEW_COMPONENT (0.34)<br>UG_CREATE_FEATURE_TEMPLATE (0.34)<br>UG_CAM_CREATE_OPERATION (0.33) |
| Replace Reference Set | assembly | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.40)<br>UG_REPLACE_FEATURE_TEMPLATE (0.34) |
| Arrangements | assembly | K3 | unresolved | — |
| Create Arrangement | assembly | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.40)<br>UG_ROUTE_CREATE_ROUTE (0.40)<br>UG_SIM_CREATE_CONSTRAINT (0.40) |
| Assembly Sequence | assembly | K3 | unresolved | UG_ASSEMBLIES_CONSTRAINTS (0.46)<br>UG_ASSEMBLIES_NAVIGATOR (0.44) |
| Exploded View | assembly | K4 | unresolved | UG_PMI_MODEL_VIEW (0.45)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_BASE_VIEW (0.39) |
| Edit Explosion | assembly | K4 | unresolved | UG_PMI_EDIT (0.40)<br>UG_ROUTE_EDIT_ROUTE (0.37)<br>UG_EDIT_PASTE (0.27) |
| Suppress Component | assembly | K4 | unresolved | UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_PATTERN_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| Component Groups | assembly | K4 | unresolved | UG_ASSEMBLIES_ADD_COMPONENT (0.36)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.33)<br>UG_SEL_COMPONENT_PRIORITY (0.32) |
| Assembly Family | assembly | K3 | unresolved | UG_ASSEMBLIES_NAVIGATOR (0.48)<br>UG_ASSEMBLIES_CONSTRAINTS (0.44)<br>UG_APP_ASSEMBLIES (0.27) |
| Product Configurator | assembly | K3 | unresolved | — |
| Clearance Analysis | assembly | K4 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35) |
| Interference Check | assembly | K5 | unresolved | UG_SKETCH_CHECKER (0.40) |
| Assembly Weight | assembly | K3 | unresolved | UG_ASSEMBLIES_NAVIGATOR (0.51)<br>UG_ASSEMBLIES_CONSTRAINTS (0.44)<br>UG_APP_ASSEMBLIES (0.26) |
| Component Where-Used | assembly | K4 | unresolved | UG_SEL_COMPONENT_PRIORITY (0.30)<br>UG_ASSEMBLIES_ADD_COMPONENT (0.29)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.26) |
| Assembly Load Options | assembly | K3 | unresolved | UG_ASSEMBLIES_CONSTRAINTS (0.42)<br>UG_ASSEMBLIES_NAVIGATOR (0.42)<br>UG_SIM_CREATE_LOAD (0.27) |
| Lightweight Representation | assembly | K3 | unresolved | — |
| Assembly Revisions | assembly | K3 | unresolved | UG_ASSEMBLIES_NAVIGATOR (0.48)<br>UG_ASSEMBLIES_CONSTRAINTS (0.46) |
| Create Frame | assembly | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.47)<br>UG_SIM_CREATE_LOAD (0.47)<br>UG_CAM_CREATE_TOOL (0.44) |
| Place Member | assembly | K3 | unresolved | UG_ROUTE_PLACE_PART (0.40) |
| Member Path | assembly | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.31)<br>UG_CAM_VERIFY_TOOL_PATH (0.30) |
| Frame Drawing Assistant | assembly | K3 | unresolved | — |
| Profile Library | assembly | K3 | unresolved | UG_MOLD_LIBRARY (0.47)<br>UG_NAVIGATOR_REUSE_LIBRARY (0.47)<br>UG_MATERIAL_LIBRARY_MANAGER (0.30) |
| Replace Profile | assembly | K3 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.45)<br>UG_REPLACE_FEATURE_TEMPLATE (0.34) |
| Update Structure | assembly | K3 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.38) |
| Initialize Fixture | assembly | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.45) |
| Add Workpiece | assembly | K3 | unresolved | UG_ASSEMBLIES_ADD_COMPONENT (0.42)<br>UG_ROUTE_ADD_STOCK (0.35) |
| Fixture Component Library | assembly | K3 | unresolved | UG_ASSEMBLIES_PATTERN_COMPONENT (0.38)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.36)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.36) |
| Place Standard Component | assembly | K3 | unresolved | UG_ASSEMBLIES_PATTERN_COMPONENT (0.42)<br>UG_ASSEMBLIES_ADD_COMPONENT (0.41)<br>UG_ASSEMBLIES_REPLACE_COMPONENT (0.41) |
| Accessibility Check | assembly | K3 | unresolved | UG_SKETCH_CHECKER (0.40) |
| Fixture Drawing | assembly | K3 | unresolved | — |
| Fixture BOM | assembly | K3 | unresolved | — |
| Check In | assembly | K3 | unresolved | — |
| Check Out | assembly | K3 | unresolved | — |
| Cancel Check Out | assembly | K3 | unresolved | — |
| Impact Analysis | assembly | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | assembly | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | assembly | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | assembly | K3 | unresolved | — |
| Import Parasolid | assembly | K4 | unresolved | — |
| Import STEP | assembly | K4 | unresolved | — |
| Import IGES | assembly | K4 | unresolved | — |
| Import JT | assembly | K4 | unresolved | — |
| Import CATIA | assembly | K4 | unresolved | — |
| Import Creo | assembly | K4 | unresolved | — |
| Import SolidWorks | assembly | K4 | unresolved | — |
| Import DXF/DWG | assembly | K4 | unresolved | — |
| Import STL | assembly | K4 | unresolved | — |
| Import OBJ | assembly | K4 | unresolved | — |
| Import IFC | assembly | K4 | unresolved | — |
| Import XML | assembly | K4 | unresolved | — |
| Export Parasolid | assembly | K4 | unresolved | — |
| Export STEP AP203/214/242 | assembly | K4 | unresolved | — |
| Export IGES | assembly | K4 | unresolved | — |
| Export JT | assembly | K4 | unresolved | — |
| Export DXF/DWG | assembly | K4 | unresolved | — |
| Export STL | assembly | K4 | unresolved | — |
| Export 3MF | assembly | K4 | unresolved | — |
| Export PDF | assembly | K4 | unresolved | — |
| Export CGM | assembly | K4 | unresolved | — |
| Export QIF | assembly | K4 | unresolved | — |
| Publish Technical Data Package | assembly | K3 | unresolved | — |
| Heal Geometry | assembly | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | assembly | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | assembly | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | assembly | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | assembly | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Place Equipment | assembly | K3 | unresolved | UG_ROUTE_PLACE_PART (0.42)<br>UG_ASSEMBLIES_REPLACE_COMPONENT (0.26) |
| Resource Library | assembly | K3 | unresolved | UG_NAVIGATOR_REUSE_LIBRARY (0.51)<br>UG_MOLD_LIBRARY (0.43)<br>UG_MATERIAL_LIBRARY_MANAGER (0.29) |
| Assign Process | assembly | K3 | unresolved | UG_MATERIAL_ASSIGN (0.39) |
| Assign Resource | assembly | K3 | unresolved | UG_MATERIAL_ASSIGN (0.39) |
| Operation Balance | assembly | K3 | unresolved | UG_CAM_OPERATION_NAVIGATOR (0.46)<br>UG_CAM_CREATE_OPERATION (0.29)<br>UG_CAM_DELETE_OPERATION (0.27) |
| Process Simulate Connection | assembly | K3 | unresolved | — |
| Ship Drawing | assembly | K3 | unresolved | — |
| Edit Journal | assembly | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | assembly | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | assembly | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | assembly | K3 | unresolved | — |
| Close | drafting | K3 | unresolved | — |
| Close All | drafting | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | drafting | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | drafting | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | drafting | K3 | unresolved | — |
| Print | drafting | K3 | unresolved | — |
| Export | drafting | K4 | unresolved | — |
| Import | drafting | K4 | unresolved | — |
| Recently Opened Parts | drafting | K3 | unresolved | — |
| Switch Window | drafting | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | drafting | K3 | unresolved | — |
| Rename | drafting | K3 | unresolved | — |
| Object Properties | drafting | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | drafting | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | drafting | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | drafting | K3 | unresolved | — |
| Unsuppress | drafting | K3 | unresolved | — |
| Reorder | drafting | K3 | unresolved | — |
| Make Current Feature | drafting | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | drafting | K3 | unresolved | — |
| Selection Filter | drafting | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | drafting | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | drafting | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | drafting | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | drafting | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | drafting | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | drafting | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | drafting | K5 | unresolved | — |
| Pan | drafting | K5 | unresolved | — |
| Rotate | drafting | K5 | unresolved | — |
| Orient View | drafting | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | drafting | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | drafting | K3 | unresolved | — |
| Back | drafting | K3 | unresolved | — |
| Top | drafting | K3 | unresolved | — |
| Bottom | drafting | K3 | unresolved | — |
| Left | drafting | K3 | unresolved | — |
| Right | drafting | K3 | unresolved | — |
| Previous View | drafting | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | drafting | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | drafting | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | drafting | K3 | unresolved | — |
| Hide | drafting | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | drafting | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | drafting | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | drafting | K3 | unresolved | — |
| Shaded | drafting | K3 | unresolved | — |
| Shaded with Edges | drafting | K3 | unresolved | — |
| Examine Geometry | drafting | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | drafting | K3 | unresolved | — |
| Customize | drafting | K3 | unresolved | — |
| Roles | drafting | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | drafting | K3 | unresolved | — |
| Part Navigator | drafting | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Drawing Sheet | drafting | K4 | unresolved | UG_MODELING_TRIM_SHEET_FEATURE (0.45)<br>UG_MODELING_FF_EXTEND_SHEET (0.42)<br>UG_MODELING_SHEET_FEATURE (0.27) |
| Sheet from Template | drafting | K3 | unresolved | UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE (0.40)<br>UG_CREATE_FEATURE_TEMPLATE (0.35)<br>UG_APP_SHEETMETAL (0.34) |
| Auxiliary View | drafting | K4 | unresolved | UG_DRAFTING_BASE_VIEW (0.41)<br>UG_DRAFTING_DETAIL_VIEW (0.41)<br>UG_DRAFTING_SECTION_VIEW (0.41) |
| Half Section | drafting | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.31) |
| Revolved Section | drafting | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.31)<br>UG_MODELING_REVOLVED_FEATURE (0.28) |
| Break-out Section | drafting | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.29)<br>UG_SKETCH_RAPID_DIMENSION (0.27) |
| Broken View | drafting | K4 | unresolved | UG_DRAFTING_BASE_VIEW (0.50)<br>UG_DRAFTING_PROJECTED_VIEW (0.47)<br>UG_PMI_MODEL_VIEW (0.46) |
| View Break | drafting | K4 | unresolved | UG_DRAFTING_VIEW_STYLE (0.44)<br>UG_VIEW_REFRESH (0.34)<br>UG_DRAFTING_BASE_VIEW (0.31) |
| View Dependent Edit | drafting | K4 | unresolved | UG_DRAFTING_VIEW_STYLE (0.31)<br>UG_PMI_EDIT (0.27)<br>UG_VIEW_POPUP_ORIENT_TFRTRI (0.25) |
| Edit View | drafting | K5 | unresolved | UG_DRAFTING_DETAIL_VIEW (0.50)<br>UG_DRAFTING_SECTION_VIEW (0.47)<br>UG_DRAFTING_BASE_VIEW (0.46) |
| Align Views | drafting | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44) |
| Move View | drafting | K4 | unresolved | UG_PMI_MODEL_VIEW (0.53)<br>UG_DRAFTING_BASE_VIEW (0.51)<br>UG_DRAFTING_PROJECTED_VIEW (0.44) |
| View Boundary | drafting | K4 | unresolved | UG_DRAFTING_VIEW_STYLE (0.39)<br>UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33)<br>UG_DRAFTING_SECTION_VIEW (0.29) |
| Drawing View Wizard | drafting | K4 | unresolved | UG_DRAFTING_VIEW_STYLE (0.34)<br>UG_DRAFTING_DETAIL_VIEW (0.33)<br>UG_DRAFTING_BASE_VIEW (0.31) |
| Horizontal Dimension | drafting | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.47)<br>UG_DRAFTING_RAPID_DIMENSION (0.46)<br>UG_SKETCH_HORIZONTAL_CONSTRAINT (0.43) |
| Vertical Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.48)<br>UG_PMI_RAPID_DIMENSION (0.45)<br>UG_SKETCH_LINEAR_DIMENSION (0.45) |
| Parallel Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.51)<br>UG_PMI_RAPID_DIMENSION (0.47)<br>UG_SKETCH_RAPID_DIMENSION (0.47) |
| Perpendicular Dimension | drafting | K4 | unresolved | UG_SKETCH_PERPENDICULAR_CONSTRAINT (0.46)<br>UG_DRAFTING_RAPID_DIMENSION (0.45)<br>UG_SKETCH_LINEAR_DIMENSION (0.43) |
| Angular Dimension | drafting | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.49)<br>UG_DRAFTING_RAPID_DIMENSION (0.48)<br>UG_PMI_RAPID_DIMENSION (0.44) |
| Radial Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.57)<br>UG_PMI_RAPID_DIMENSION (0.53)<br>UG_SKETCH_RAPID_DIMENSION (0.53) |
| Diameter Dimension | drafting | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.50)<br>UG_DRAFTING_RAPID_DIMENSION (0.48)<br>UG_PMI_RAPID_DIMENSION (0.45) |
| Ordinate Dimension | drafting | K4 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.49)<br>UG_DRAFTING_RAPID_DIMENSION (0.48)<br>UG_PMI_RAPID_DIMENSION (0.45) |
| Baseline Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.51)<br>UG_SKETCH_RAPID_DIMENSION (0.49)<br>UG_PMI_RAPID_DIMENSION (0.47) |
| Chain Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.54)<br>UG_PMI_RAPID_DIMENSION (0.50)<br>UG_SKETCH_RAPID_DIMENSION (0.50) |
| Coordinate Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.46)<br>UG_SKETCH_LINEAR_DIMENSION (0.45)<br>UG_PMI_RAPID_DIMENSION (0.42) |
| Feature Parameters | drafting | K4 | unresolved | UG_MODELING_PATTERNFEATURE_FEATURE (0.33)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.31) |
| Retrieve Dimensions | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.30)<br>UG_PMI_RAPID_DIMENSION (0.27)<br>UG_SKETCH_RAPID_DIMENSION (0.27) |
| Reassociate Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.49)<br>UG_PMI_RAPID_DIMENSION (0.45)<br>UG_SKETCH_RAPID_DIMENSION (0.45) |
| Convert to Reference | drafting | K3 | unresolved | UG_SKETCH_LINEAR_DIMENSION (0.44)<br>UG_SKETCH_RAPID_DIMENSION (0.42)<br>UG_LAYER_MOVE (0.30) |
| Edit Dimension | drafting | K4 | unresolved | UG_DRAFTING_RAPID_DIMENSION (0.54)<br>UG_PMI_RAPID_DIMENSION (0.50)<br>UG_SKETCH_RAPID_DIMENSION (0.50) |
| Label | drafting | K3 | unresolved | — |
| ID Symbol | drafting | K3 | unresolved | UG_PMI_SURFACE_FINISH (0.30)<br>UG_PMI_DATUM_FEATURE_SYMBOL (0.29) |
| Balloon | drafting | K3 | unresolved | — |
| Weld Symbol | drafting | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.31)<br>UG_PMI_SURFACE_FINISH (0.30) |
| Center Mark | drafting | K3 | unresolved | — |
| Intersection Symbol | drafting | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_PMI_SURFACE_FINISH (0.32) |
| Custom Symbol | drafting | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.33)<br>UG_PMI_SURFACE_FINISH (0.30) |
| Tabular Note | drafting | K3 | unresolved | UG_PMI_NOTE (0.37) |
| Revision Table | drafting | K3 | unresolved | UG_PARAMETER_TABLE (0.36) |
| Title Block | drafting | K3 | unresolved | — |
| Populate Title Block | drafting | K3 | unresolved | — |
| Edit Annotation | drafting | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.39)<br>UG_PMI_EDIT (0.36)<br>UG_EDIT_UNDO (0.26) |
| Distribute Annotations | drafting | K3 | unresolved | — |
| Edit Settings | drafting | K4 | unresolved | UG_LAYER_SETTINGS (0.46)<br>UG_PMI_EDIT (0.39)<br>UG_ROUTE_EDIT_ROUTE (0.39) |
| Inherit Settings | drafting | K3 | unresolved | UG_LAYER_SETTINGS (0.48) |
| Drawing Automation | drafting | K4 | unresolved | — |
| Drawing Creation Wizard | drafting | K4 | unresolved | UG_APP_MOLDWIZARD (0.27) |
| Smash Drawing View | drafting | K4 | unresolved | UG_DRAFTING_BASE_VIEW (0.34)<br>UG_DRAFTING_DETAIL_VIEW (0.34)<br>UG_DRAFTING_SECTION_VIEW (0.34) |
| Move to Drawing View | drafting | K4 | unresolved | UG_LAYER_MOVE (0.44)<br>UG_APP_GATEWAY (0.37)<br>UG_DRAFTING_DETAIL_VIEW (0.34) |
| Export PDF | drafting | K4 | unresolved | — |
| Export DXF/DWG | drafting | K4 | unresolved | — |
| Plot | drafting | K3 | unresolved | — |
| Check In | drafting | K3 | unresolved | — |
| Check Out | drafting | K3 | unresolved | — |
| Cancel Check Out | drafting | K3 | unresolved | — |
| Impact Analysis | drafting | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | drafting | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | drafting | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | drafting | K3 | unresolved | — |
| Import Parasolid | drafting | K4 | unresolved | — |
| Import STEP | drafting | K4 | unresolved | — |
| Import IGES | drafting | K4 | unresolved | — |
| Import JT | drafting | K4 | unresolved | — |
| Import CATIA | drafting | K4 | unresolved | — |
| Import Creo | drafting | K4 | unresolved | — |
| Import SolidWorks | drafting | K4 | unresolved | — |
| Import DXF/DWG | drafting | K4 | unresolved | — |
| Import STL | drafting | K4 | unresolved | — |
| Import OBJ | drafting | K4 | unresolved | — |
| Import IFC | drafting | K4 | unresolved | — |
| Import XML | drafting | K4 | unresolved | — |
| Export Parasolid | drafting | K4 | unresolved | — |
| Export STEP AP203/214/242 | drafting | K4 | unresolved | — |
| Export IGES | drafting | K4 | unresolved | — |
| Export JT | drafting | K4 | unresolved | — |
| Export STL | drafting | K4 | unresolved | — |
| Export 3MF | drafting | K4 | unresolved | — |
| Export CGM | drafting | K4 | unresolved | — |
| Export QIF | drafting | K4 | unresolved | — |
| Publish Technical Data Package | drafting | K3 | unresolved | — |
| Heal Geometry | drafting | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | drafting | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | drafting | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | drafting | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | drafting | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | drafting | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | drafting | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | drafting | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | drafting | K3 | unresolved | — |
| Close | pmi | K3 | unresolved | — |
| Close All | pmi | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | pmi | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | pmi | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | pmi | K3 | unresolved | — |
| Print | pmi | K3 | unresolved | — |
| Export | pmi | K4 | unresolved | — |
| Import | pmi | K4 | unresolved | — |
| Recently Opened Parts | pmi | K3 | unresolved | — |
| Switch Window | pmi | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | pmi | K3 | unresolved | — |
| Rename | pmi | K3 | unresolved | — |
| Object Properties | pmi | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | pmi | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | pmi | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | pmi | K3 | unresolved | — |
| Unsuppress | pmi | K3 | unresolved | — |
| Reorder | pmi | K3 | unresolved | — |
| Make Current Feature | pmi | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | pmi | K3 | unresolved | — |
| Selection Filter | pmi | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | pmi | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | pmi | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | pmi | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | pmi | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | pmi | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | pmi | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | pmi | K5 | unresolved | — |
| Pan | pmi | K5 | unresolved | — |
| Rotate | pmi | K5 | unresolved | — |
| Orient View | pmi | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | pmi | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | pmi | K3 | unresolved | — |
| Back | pmi | K3 | unresolved | — |
| Top | pmi | K3 | unresolved | — |
| Bottom | pmi | K3 | unresolved | — |
| Left | pmi | K3 | unresolved | — |
| Right | pmi | K3 | unresolved | — |
| Previous View | pmi | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | pmi | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | pmi | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | pmi | K3 | unresolved | — |
| Hide | pmi | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | pmi | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | pmi | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | pmi | K3 | unresolved | — |
| Shaded | pmi | K3 | unresolved | — |
| Shaded with Edges | pmi | K3 | unresolved | — |
| Examine Geometry | pmi | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | pmi | K3 | unresolved | — |
| Customize | pmi | K3 | unresolved | — |
| Roles | pmi | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | pmi | K3 | unresolved | — |
| Part Navigator | pmi | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| PMI Rapid Dimension | pmi | K4 | ambiguous | UG_PMI_RAPID_DIMENSION (0.99)<br>UG_DRAFTING_RAPID_DIMENSION (0.94)<br>UG_SKETCH_RAPID_DIMENSION (0.94) |
| PMI Linear Dimension | pmi | K4 | ambiguous | UG_SKETCH_LINEAR_DIMENSION (0.94)<br>UG_APP_PMI (0.87)<br>UG_PMI_RAPID_DIMENSION (0.52) |
| Datum Target | pmi | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_SEL_DATUM_PRIORITY (0.30) |
| Weld Symbol | pmi | K3 | unresolved | UG_PMI_DATUM_FEATURE_SYMBOL (0.35)<br>UG_PMI_SURFACE_FINISH (0.34) |
| Coordinate Note | pmi | K3 | unresolved | UG_PMI_NOTE (0.40) |
| Annotation Plane | pmi | K3 | unresolved | — |
| Automatic Annotation Plane | pmi | K3 | unresolved | — |
| Convert Drafting to PMI | pmi | K4 | ambiguous | UG_APP_DRAFTING (0.85)<br>UG_APP_PMI (0.84)<br>UG_PMI_EDIT (0.30) |
| Model-Based Characteristics | pmi | K3 | unresolved | UG_PMI_MODEL_VIEW (0.29) |
| NX Inspector | pmi | K3 | unresolved | — |
| Characteristics Navigator | pmi | K3 | unresolved | UG_CAM_OPERATION_NAVIGATOR (0.43)<br>UG_NAVIGATOR_PART (0.41)<br>UG_ROUTE_NAVIGATOR (0.41) |
| Assign Inspection Requirement | pmi | K4 | unresolved | UG_MATERIAL_ASSIGN (0.29) |
| Export QIF | pmi | K4 | unresolved | — |
| Export STEP AP242 | pmi | K4 | unresolved | — |
| Check In | pmi | K3 | unresolved | — |
| Check Out | pmi | K3 | unresolved | — |
| Cancel Check Out | pmi | K3 | unresolved | — |
| Impact Analysis | pmi | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | pmi | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | pmi | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | pmi | K3 | unresolved | — |
| Import Parasolid | pmi | K4 | unresolved | — |
| Import STEP | pmi | K4 | unresolved | — |
| Import IGES | pmi | K4 | unresolved | — |
| Import JT | pmi | K4 | unresolved | — |
| Import CATIA | pmi | K4 | unresolved | — |
| Import Creo | pmi | K4 | unresolved | — |
| Import SolidWorks | pmi | K4 | unresolved | — |
| Import DXF/DWG | pmi | K4 | unresolved | — |
| Import STL | pmi | K4 | unresolved | — |
| Import OBJ | pmi | K4 | unresolved | — |
| Import IFC | pmi | K4 | unresolved | — |
| Import XML | pmi | K4 | unresolved | — |
| Export Parasolid | pmi | K4 | unresolved | — |
| Export STEP AP203/214/242 | pmi | K4 | unresolved | — |
| Export IGES | pmi | K4 | unresolved | — |
| Export JT | pmi | K4 | unresolved | — |
| Export DXF/DWG | pmi | K4 | unresolved | — |
| Export STL | pmi | K4 | unresolved | — |
| Export 3MF | pmi | K4 | unresolved | — |
| Export PDF | pmi | K4 | unresolved | — |
| Export CGM | pmi | K4 | unresolved | — |
| Publish Technical Data Package | pmi | K3 | unresolved | — |
| Heal Geometry | pmi | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | pmi | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | pmi | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | pmi | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | pmi | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | pmi | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | pmi | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | pmi | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | pmi | K3 | unresolved | — |
| Close | surface | K3 | unresolved | — |
| Close All | surface | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | surface | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | surface | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | surface | K3 | unresolved | — |
| Print | surface | K3 | unresolved | — |
| Export | surface | K4 | unresolved | — |
| Import | surface | K4 | unresolved | — |
| Recently Opened Parts | surface | K3 | unresolved | — |
| Switch Window | surface | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | surface | K3 | unresolved | — |
| Rename | surface | K3 | unresolved | — |
| Object Properties | surface | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | surface | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | surface | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | surface | K3 | unresolved | — |
| Unsuppress | surface | K3 | unresolved | — |
| Reorder | surface | K3 | unresolved | — |
| Make Current Feature | surface | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | surface | K3 | unresolved | — |
| Selection Filter | surface | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | surface | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | surface | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | surface | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | surface | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | surface | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | surface | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | surface | K5 | unresolved | — |
| Pan | surface | K5 | unresolved | — |
| Rotate | surface | K5 | unresolved | — |
| Orient View | surface | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | surface | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | surface | K3 | unresolved | — |
| Back | surface | K3 | unresolved | — |
| Top | surface | K3 | unresolved | — |
| Bottom | surface | K3 | unresolved | — |
| Left | surface | K3 | unresolved | — |
| Right | surface | K3 | unresolved | — |
| Previous View | surface | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | surface | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | surface | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | surface | K3 | unresolved | — |
| Hide | surface | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | surface | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | surface | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | surface | K3 | unresolved | — |
| Shaded | surface | K3 | unresolved | — |
| Shaded with Edges | surface | K3 | unresolved | — |
| Examine Geometry | surface | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | surface | K3 | unresolved | — |
| Customize | surface | K3 | unresolved | — |
| Roles | surface | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | surface | K3 | unresolved | — |
| Part Navigator | surface | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Create Subdivision Body | surface | K3 | unresolved | UG_SIM_CREATE_SOLUTION (0.35)<br>UG_CAM_CREATE_OPERATION (0.33)<br>UG_CAM_CREATE_TOOL (0.31) |
| Mirror Cage | surface | K3 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.46) |
| Reflection Analysis | surface | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35) |
| Curvature Analysis | surface | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35)<br>UG_ANALYSIS_FACE_CURVATURE (0.34) |
| Draft Analysis | surface | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Deviation Analysis | surface | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35) |
| Section Analysis | surface | K3 | unresolved | UG_DRAFTING_SECTION_VIEW (0.40)<br>UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.40) |
| Check In | surface | K3 | unresolved | — |
| Check Out | surface | K3 | unresolved | — |
| Cancel Check Out | surface | K3 | unresolved | — |
| Impact Analysis | surface | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | surface | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | surface | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | surface | K3 | unresolved | — |
| Import Parasolid | surface | K4 | unresolved | — |
| Import STEP | surface | K4 | unresolved | — |
| Import IGES | surface | K4 | unresolved | — |
| Import JT | surface | K4 | unresolved | — |
| Import CATIA | surface | K4 | unresolved | — |
| Import Creo | surface | K4 | unresolved | — |
| Import SolidWorks | surface | K4 | unresolved | — |
| Import DXF/DWG | surface | K4 | unresolved | — |
| Import STL | surface | K4 | unresolved | — |
| Import OBJ | surface | K4 | unresolved | — |
| Import IFC | surface | K4 | unresolved | — |
| Import XML | surface | K4 | unresolved | — |
| Export Parasolid | surface | K4 | unresolved | — |
| Export STEP AP203/214/242 | surface | K4 | unresolved | — |
| Export IGES | surface | K4 | unresolved | — |
| Export JT | surface | K4 | unresolved | — |
| Export DXF/DWG | surface | K4 | unresolved | — |
| Export STL | surface | K4 | unresolved | — |
| Export 3MF | surface | K4 | unresolved | — |
| Export PDF | surface | K4 | unresolved | — |
| Export CGM | surface | K4 | unresolved | — |
| Export QIF | surface | K4 | unresolved | — |
| Publish Technical Data Package | surface | K3 | unresolved | — |
| Heal Geometry | surface | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | surface | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | surface | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | surface | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | surface | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | surface | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | surface | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | surface | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | surface | K3 | unresolved | — |
| Close | sheet_metal | K3 | unresolved | — |
| Close All | sheet_metal | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | sheet_metal | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | sheet_metal | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | sheet_metal | K3 | unresolved | — |
| Print | sheet_metal | K3 | unresolved | — |
| Export | sheet_metal | K4 | unresolved | — |
| Import | sheet_metal | K4 | unresolved | — |
| Recently Opened Parts | sheet_metal | K3 | unresolved | — |
| Switch Window | sheet_metal | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | sheet_metal | K3 | unresolved | — |
| Rename | sheet_metal | K3 | unresolved | — |
| Object Properties | sheet_metal | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | sheet_metal | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | sheet_metal | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | sheet_metal | K3 | unresolved | — |
| Unsuppress | sheet_metal | K3 | unresolved | — |
| Reorder | sheet_metal | K3 | unresolved | — |
| Make Current Feature | sheet_metal | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | sheet_metal | K3 | unresolved | — |
| Selection Filter | sheet_metal | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | sheet_metal | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | sheet_metal | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | sheet_metal | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | sheet_metal | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | sheet_metal | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | sheet_metal | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | sheet_metal | K5 | unresolved | — |
| Pan | sheet_metal | K5 | unresolved | — |
| Rotate | sheet_metal | K5 | unresolved | — |
| Orient View | sheet_metal | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | sheet_metal | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | sheet_metal | K3 | unresolved | — |
| Back | sheet_metal | K3 | unresolved | — |
| Top | sheet_metal | K3 | unresolved | — |
| Bottom | sheet_metal | K3 | unresolved | — |
| Left | sheet_metal | K3 | unresolved | — |
| Right | sheet_metal | K3 | unresolved | — |
| Previous View | sheet_metal | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | sheet_metal | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | sheet_metal | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | sheet_metal | K3 | unresolved | — |
| Hide | sheet_metal | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | sheet_metal | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | sheet_metal | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | sheet_metal | K3 | unresolved | — |
| Shaded | sheet_metal | K3 | unresolved | — |
| Shaded with Edges | sheet_metal | K3 | unresolved | — |
| Examine Geometry | sheet_metal | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | sheet_metal | K3 | unresolved | — |
| Customize | sheet_metal | K3 | unresolved | — |
| Roles | sheet_metal | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | sheet_metal | K3 | unresolved | — |
| Part Navigator | sheet_metal | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Jog | sheet_metal | K3 | unresolved | — |
| Hem | sheet_metal | K3 | unresolved | — |
| Closed Corner | sheet_metal | K3 | unresolved | — |
| Break Corner | sheet_metal | K3 | unresolved | — |
| Normal Cutout | sheet_metal | K3 | unresolved | — |
| Bead | sheet_metal | K3 | unresolved | UG_SHEET_METAL_BEND (0.35) |
| Dimple | sheet_metal | K3 | unresolved | — |
| Louver | sheet_metal | K3 | unresolved | — |
| Drawn Cutout | sheet_metal | K3 | unresolved | — |
| Convert Utility | sheet_metal | K3 | unresolved | — |
| Recognize Bends | sheet_metal | K4 | unresolved | — |
| Flat Solid | sheet_metal | K4 | unresolved | UG_SHEET_METAL_FLAT_PATTERN (0.40)<br>UG_SBSM_SHEETMETAL_FROM_SOLID_FEATURE (0.28) |
| Analyze Formability | sheet_metal | K3 | unresolved | — |
| Edit Corner Relief | sheet_metal | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.29)<br>UG_EDIT_BLANK_SELECTED (0.27) |
| Check In | sheet_metal | K3 | unresolved | — |
| Check Out | sheet_metal | K3 | unresolved | — |
| Cancel Check Out | sheet_metal | K3 | unresolved | — |
| Impact Analysis | sheet_metal | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | sheet_metal | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | sheet_metal | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | sheet_metal | K3 | unresolved | — |
| Import Parasolid | sheet_metal | K4 | unresolved | — |
| Import STEP | sheet_metal | K4 | unresolved | — |
| Import IGES | sheet_metal | K4 | unresolved | — |
| Import JT | sheet_metal | K4 | unresolved | — |
| Import CATIA | sheet_metal | K4 | unresolved | — |
| Import Creo | sheet_metal | K4 | unresolved | — |
| Import SolidWorks | sheet_metal | K4 | unresolved | — |
| Import DXF/DWG | sheet_metal | K4 | unresolved | — |
| Import STL | sheet_metal | K4 | unresolved | — |
| Import OBJ | sheet_metal | K4 | unresolved | — |
| Import IFC | sheet_metal | K4 | unresolved | — |
| Import XML | sheet_metal | K4 | unresolved | — |
| Export Parasolid | sheet_metal | K4 | unresolved | — |
| Export STEP AP203/214/242 | sheet_metal | K4 | unresolved | — |
| Export IGES | sheet_metal | K4 | unresolved | — |
| Export JT | sheet_metal | K4 | unresolved | — |
| Export DXF/DWG | sheet_metal | K4 | unresolved | — |
| Export STL | sheet_metal | K4 | unresolved | — |
| Export 3MF | sheet_metal | K4 | unresolved | — |
| Export PDF | sheet_metal | K4 | unresolved | — |
| Export CGM | sheet_metal | K4 | unresolved | — |
| Export QIF | sheet_metal | K4 | unresolved | — |
| Publish Technical Data Package | sheet_metal | K3 | unresolved | — |
| Heal Geometry | sheet_metal | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | sheet_metal | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | sheet_metal | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | sheet_metal | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | sheet_metal | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | sheet_metal | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | sheet_metal | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | sheet_metal | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | sheet_metal | K3 | unresolved | — |
| Close | manufacturing | K3 | unresolved | — |
| Close All | manufacturing | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | manufacturing | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | manufacturing | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | manufacturing | K3 | unresolved | — |
| Print | manufacturing | K3 | unresolved | — |
| Export | manufacturing | K4 | unresolved | — |
| Import | manufacturing | K4 | unresolved | — |
| Recently Opened Parts | manufacturing | K3 | unresolved | — |
| Switch Window | manufacturing | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | manufacturing | K3 | unresolved | — |
| Rename | manufacturing | K3 | unresolved | — |
| Object Properties | manufacturing | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | manufacturing | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | manufacturing | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | manufacturing | K3 | unresolved | — |
| Unsuppress | manufacturing | K3 | unresolved | — |
| Reorder | manufacturing | K3 | unresolved | — |
| Make Current Feature | manufacturing | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | manufacturing | K3 | unresolved | — |
| Selection Filter | manufacturing | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | manufacturing | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | manufacturing | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | manufacturing | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | manufacturing | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | manufacturing | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | manufacturing | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | manufacturing | K5 | unresolved | — |
| Pan | manufacturing | K5 | unresolved | — |
| Rotate | manufacturing | K5 | unresolved | — |
| Orient View | manufacturing | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | manufacturing | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | manufacturing | K3 | unresolved | — |
| Back | manufacturing | K3 | unresolved | — |
| Top | manufacturing | K3 | unresolved | — |
| Bottom | manufacturing | K3 | unresolved | — |
| Left | manufacturing | K3 | unresolved | — |
| Right | manufacturing | K3 | unresolved | — |
| Previous View | manufacturing | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | manufacturing | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | manufacturing | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | manufacturing | K3 | unresolved | — |
| Hide | manufacturing | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | manufacturing | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | manufacturing | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | manufacturing | K3 | unresolved | — |
| Shaded | manufacturing | K3 | unresolved | — |
| Shaded with Edges | manufacturing | K3 | unresolved | — |
| Examine Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | manufacturing | K3 | unresolved | — |
| Customize | manufacturing | K3 | unresolved | — |
| Roles | manufacturing | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | manufacturing | K3 | unresolved | — |
| Part Navigator | manufacturing | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Check In | manufacturing | K3 | unresolved | — |
| Check Out | manufacturing | K3 | unresolved | — |
| Cancel Check Out | manufacturing | K3 | unresolved | — |
| Impact Analysis | manufacturing | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | manufacturing | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | manufacturing | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | manufacturing | K3 | unresolved | — |
| Import Parasolid | manufacturing | K4 | unresolved | — |
| Import STEP | manufacturing | K4 | unresolved | — |
| Import IGES | manufacturing | K4 | unresolved | — |
| Import JT | manufacturing | K4 | unresolved | — |
| Import CATIA | manufacturing | K4 | unresolved | — |
| Import Creo | manufacturing | K4 | unresolved | — |
| Import SolidWorks | manufacturing | K4 | unresolved | — |
| Import DXF/DWG | manufacturing | K4 | unresolved | — |
| Import STL | manufacturing | K4 | unresolved | — |
| Import OBJ | manufacturing | K4 | unresolved | — |
| Import IFC | manufacturing | K4 | unresolved | — |
| Import XML | manufacturing | K4 | unresolved | — |
| Export Parasolid | manufacturing | K4 | unresolved | — |
| Export STEP AP203/214/242 | manufacturing | K4 | unresolved | — |
| Export IGES | manufacturing | K4 | unresolved | — |
| Export JT | manufacturing | K4 | unresolved | — |
| Export DXF/DWG | manufacturing | K4 | unresolved | — |
| Export STL | manufacturing | K4 | unresolved | — |
| Export 3MF | manufacturing | K4 | unresolved | — |
| Export PDF | manufacturing | K4 | unresolved | — |
| Export CGM | manufacturing | K4 | unresolved | — |
| Export QIF | manufacturing | K4 | unresolved | — |
| Publish Technical Data Package | manufacturing | K3 | unresolved | — |
| Heal Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | manufacturing | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | manufacturing | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Create Program | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.47)<br>UG_CAM_CREATE_OPERATION (0.46)<br>UG_ROUTE_CREATE_ROUTE (0.46) |
| Create Geometry | manufacturing | K5 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_CAM_CREATE_TOOL (0.45)<br>UG_ROUTE_CREATE_ROUTE (0.45) |
| Create Method | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.52)<br>UG_CAM_CREATE_OPERATION (0.49)<br>UG_SIM_CREATE_LOAD (0.45) |
| Program Order View | manufacturing | K4 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.35)<br>UG_PMI_MODEL_VIEW (0.35)<br>UG_LAYER_VIEW (0.33) |
| Machine Tool View | manufacturing | K4 | unresolved | UG_DRAFTING_DETAIL_VIEW (0.34)<br>UG_DRAFTING_SECTION_VIEW (0.34)<br>UG_PMI_MODEL_VIEW (0.34) |
| Geometry View | manufacturing | K4 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_PMI_MODEL_VIEW (0.42)<br>UG_DRAFTING_BASE_VIEW (0.39) |
| Method View | manufacturing | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.47)<br>UG_DRAFTING_DETAIL_VIEW (0.46)<br>UG_PMI_MODEL_VIEW (0.42) |
| Clone Operation | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.54)<br>UG_CAM_DELETE_OPERATION (0.54)<br>UG_CAM_OPERATION_NAVIGATOR (0.32) |
| Group Operations | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.30)<br>UG_CAM_DELETE_OPERATION (0.27) |
| Reorder Operations | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_CAM_DELETE_OPERATION (0.32) |
| Suppress Operation | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.48)<br>UG_CAM_DELETE_OPERATION (0.48)<br>UG_CAM_OPERATION_NAVIGATOR (0.32) |
| Unsuppress Operation | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.46)<br>UG_CAM_DELETE_OPERATION (0.46)<br>UG_CAM_OPERATION_NAVIGATOR (0.29) |
| Edit Operation | manufacturing | K4 | unresolved | UG_CAM_DELETE_OPERATION (0.54)<br>UG_CAM_CREATE_OPERATION (0.52)<br>UG_PMI_EDIT (0.40) |
| MCS | manufacturing | K3 | unresolved | — |
| Workpiece | manufacturing | K4 | unresolved | — |
| Part Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_NAVIGATOR_PART (0.37)<br>UG_ASSY_WAVE_LINKER (0.35) |
| Blank Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Check Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Fixture Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.29)<br>UG_MODELING_WAVE_LINKER (0.29) |
| Drive Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.35)<br>UG_MODELING_WAVE_LINKER (0.35) |
| Containment | manufacturing | K3 | unresolved | — |
| IPW | manufacturing | K3 | unresolved | — |
| Create Blank from Part | manufacturing | K4 | unresolved | UG_SIM_CREATE_CONSTRAINT (0.31)<br>UG_SIM_CREATE_LOAD (0.31)<br>UG_CAM_CREATE_OPERATION (0.30) |
| Create Blank from Bounding Block | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.26)<br>UG_CAM_CREATE_TOOL (0.26)<br>UG_ASSEMBLIES_NEW_COMPONENT (0.25) |
| Create Blank from Part Offset | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.29)<br>UG_ASSEMBLIES_NEW_COMPONENT (0.27)<br>UG_CAM_CREATE_TOOL (0.26) |
| Define Machine Setup | manufacturing | K4 | unresolved | — |
| Mount Part | manufacturing | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.42)<br>UG_ROUTE_PLACE_PART (0.40)<br>UG_NAVIGATOR_PART (0.32) |
| Mount Fixture | manufacturing | K4 | unresolved | — |
| Tool Library | manufacturing | K4 | unresolved | UG_MOLD_LIBRARY (0.51)<br>UG_NAVIGATOR_REUSE_LIBRARY (0.45)<br>UG_CAM_INFORMATION (0.34) |
| Search Tool | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.46)<br>UG_CAM_GENERATE_TOOL_PATH (0.34)<br>UG_CAM_VERIFY_TOOL_PATH (0.34) |
| Create Drill | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.51)<br>UG_CAM_CREATE_OPERATION (0.46)<br>UG_ROUTE_CREATE_ROUTE (0.44) |
| Create Probe | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.51)<br>UG_ROUTE_CREATE_ROUTE (0.51)<br>UG_SIM_CREATE_LOAD (0.47) |
| Create Holder | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.49)<br>UG_ROUTE_CREATE_ROUTE (0.48)<br>UG_SIM_CREATE_LOAD (0.48) |
| Tool Assembly | manufacturing | K4 | unresolved | UG_APP_ASSEMBLIES (0.38)<br>UG_CAM_INFORMATION (0.32)<br>UG_ASSEMBLIES_NAVIGATOR (0.26) |
| Set Tool Number | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.29)<br>UG_CAM_GENERATE_TOOL_PATH (0.29)<br>UG_CAM_VERIFY_TOOL_PATH (0.28) |
| Cutting Data | manufacturing | K3 | unresolved | UG_ASSY_WAVE_LOAD_DATA (0.29) |
| Feeds and Speeds | manufacturing | K3 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.27) |
| Tool Tracking | manufacturing | K4 | unresolved | UG_CAM_INFORMATION (0.34)<br>UG_CAM_CREATE_TOOL (0.26) |
| CoroPlus Tool Library | manufacturing | K4 | unresolved | UG_NAVIGATOR_REUSE_LIBRARY (0.36)<br>UG_MOLD_LIBRARY (0.34)<br>UG_CAM_CREATE_TOOL (0.32) |
| Export Tool List | manufacturing | K4 | unresolved | UG_DRAFTING_PARTS_LIST (0.35)<br>UG_CAM_GENERATE_TOOL_PATH (0.34)<br>UG_CAM_CREATE_TOOL (0.31) |
| Generate Selected | manufacturing | K4 | unresolved | UG_EDIT_BLANK_SELECTED (0.44)<br>UG_CAM_GENERATE_TOOL_PATH (0.41) |
| Generate Group | manufacturing | K4 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.44) |
| Background Generate | manufacturing | K4 | unresolved | — |
| Replay Tool Path | manufacturing | K4 | ambiguous | UG_CAM_VERIFY_TOOL_PATH (0.64)<br>UG_CAM_GENERATE_TOOL_PATH (0.61)<br>UG_CAM_INFORMATION (0.43) |
| Gouge Check | manufacturing | K4 | unresolved | UG_SKETCH_CHECKER (0.46) |
| Collision Check | manufacturing | K4 | unresolved | UG_SKETCH_CHECKER (0.39) |
| Compare Tool Path | manufacturing | K4 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.61)<br>UG_CAM_VERIFY_TOOL_PATH (0.57)<br>UG_CAM_INFORMATION (0.44) |
| Tool Path Statistics | manufacturing | K4 | unresolved | UG_CAM_INFORMATION (0.57)<br>UG_CAM_GENERATE_TOOL_PATH (0.41)<br>UG_CAM_VERIFY_TOOL_PATH (0.37) |
| List Tool Path | manufacturing | K4 | unresolved | UG_CAM_VERIFY_TOOL_PATH (0.61)<br>UG_CAM_GENERATE_TOOL_PATH (0.58)<br>UG_CAM_INFORMATION (0.43) |
| Edit Tool Path | manufacturing | K4 | ambiguous | UG_CAM_VERIFY_TOOL_PATH (0.64)<br>UG_CAM_GENERATE_TOOL_PATH (0.61)<br>UG_CAM_INFORMATION (0.41) |
| Transform Tool Path | manufacturing | K4 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.57)<br>UG_CAM_VERIFY_TOOL_PATH (0.57)<br>UG_CAM_INFORMATION (0.41) |
| Pattern Tool Path | manufacturing | K4 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.58)<br>UG_CAM_VERIFY_TOOL_PATH (0.57)<br>UG_CAM_INFORMATION (0.41) |
| Post Process | manufacturing | K5 | unresolved | UG_CAM_POSTPROCESS (0.42) |
| Post Hub | manufacturing | K4 | unresolved | — |
| Post Configurator | manufacturing | K4 | unresolved | — |
| Shop Documentation | manufacturing | K3 | unresolved | — |
| Setup Sheet | manufacturing | K4 | unresolved | UG_MODELING_FF_EXTEND_SHEET (0.44)<br>UG_MODELING_TRIM_SHEET_FEATURE (0.42)<br>UG_MODELING_SHEET_FEATURE (0.29) |
| Tool List | manufacturing | K4 | unresolved | UG_DRAFTING_PARTS_LIST (0.40)<br>UG_CAM_INFORMATION (0.32)<br>UG_CAM_VERIFY_TOOL_PATH (0.31) |
| Operation List | manufacturing | K4 | unresolved | UG_CAM_OPERATION_NAVIGATOR (0.49)<br>UG_DRAFTING_PARTS_LIST (0.43)<br>UG_CAM_CREATE_OPERATION (0.33) |
| Send to Shop Floor | manufacturing | K3 | unresolved | — |
| Spot Drilling | manufacturing | K4 | unresolved | — |
| Drilling | manufacturing | K4 | unresolved | UG_APP_DRAFTING (0.26)<br>UG_SKETCH_COINCIDENT_CONSTRAINT (0.25) |
| Peck Drilling | manufacturing | K4 | unresolved | — |
| Chip Break Drilling | manufacturing | K4 | unresolved | — |
| Counterboring | manufacturing | K3 | unresolved | — |
| Countersinking | manufacturing | K3 | unresolved | — |
| Reaming | manufacturing | K3 | unresolved | — |
| Boring | manufacturing | K3 | unresolved | — |
| Back Boring | manufacturing | K3 | unresolved | — |
| Tapping | manufacturing | K3 | unresolved | — |
| Thread Milling | manufacturing | K4 | unresolved | — |
| Face Milling | manufacturing | K4 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.34)<br>UG_SEL_FACE_PRIORITY (0.29)<br>UG_APP_MODELING (0.26) |
| Planar Milling | manufacturing | K4 | unresolved | — |
| Floor and Wall | manufacturing | K3 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.30) |
| Cavity Milling | manufacturing | K4 | unresolved | — |
| Pocket Milling | manufacturing | K4 | unresolved | — |
| Profile Milling | manufacturing | K4 | unresolved | — |
| Slot Milling | manufacturing | K4 | unresolved | — |
| Zig | manufacturing | K3 | unresolved | — |
| Zig-Zag | manufacturing | K3 | unresolved | — |
| Follow Periphery | manufacturing | K3 | unresolved | — |
| Follow Part | manufacturing | K3 | unresolved | UG_ROUTE_PLACE_PART (0.42)<br>UG_ROUTE_REMOVE_PART (0.38)<br>UG_NAVIGATOR_PART (0.29) |
| Trochoidal Milling | manufacturing | K4 | unresolved | — |
| High Speed Milling | manufacturing | K4 | unresolved | — |
| Volume-Based Milling | manufacturing | K4 | unresolved | — |
| Feature-Based Machining | manufacturing | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.29)<br>UG_SKETCH_CIRCLE_BY_THREE_POINTS (0.26)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.26) |
| Floor Wall IPW | manufacturing | K3 | unresolved | — |
| Deburring | manufacturing | K3 | unresolved | — |
| Engraving | manufacturing | K3 | unresolved | — |
| 3D Adaptive Roughing | manufacturing | K3 | unresolved | — |
| Z-Level Roughing | manufacturing | K3 | unresolved | — |
| Rest Milling | manufacturing | K4 | unresolved | — |
| Plunge Milling | manufacturing | K4 | unresolved | — |
| Corner Roughing | manufacturing | K3 | unresolved | — |
| IPW Rest Roughing | manufacturing | K3 | unresolved | — |
| Fixed Contour | manufacturing | K3 | unresolved | — |
| Variable Contour | manufacturing | K3 | unresolved | — |
| Contour Area | manufacturing | K3 | unresolved | UG_SHEET_METAL_CONTOUR_FLANGE (0.46) |
| Streamline | manufacturing | K3 | unresolved | — |
| Guiding Curves | manufacturing | K4 | unresolved | UG_MODELING_THROUGH_CURVES_FEATURE (0.40) |
| Z-Level Finishing | manufacturing | K3 | unresolved | — |
| Steep and Non-Steep | manufacturing | K3 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.27) |
| Scallop | manufacturing | K3 | unresolved | — |
| Corner Finishing | manufacturing | K3 | unresolved | — |
| Pencil Milling | manufacturing | K4 | unresolved | — |
| Profile 3D | manufacturing | K3 | unresolved | — |
| Surface Area Milling | manufacturing | K4 | unresolved | UG_APP_MODELING (0.40)<br>UG_PMI_SURFACE_FINISH (0.28) |
| Specialized Finishing | manufacturing | K3 | unresolved | — |
| Smooth Connections | manufacturing | K3 | unresolved | — |
| Variable Axis Contour Milling | manufacturing | K4 | unresolved | UG_SHEET_METAL_CONTOUR_FLANGE (0.26) |
| Swarf Milling | manufacturing | K4 | unresolved | — |
| Multi Blade | manufacturing | K3 | unresolved | — |
| Tube Milling | manufacturing | K4 | unresolved | — |
| Impeller Milling | manufacturing | K4 | unresolved | — |
| Port Milling | manufacturing | K4 | unresolved | — |
| 5-Axis Flank Milling | manufacturing | K4 | unresolved | — |
| Guiding Curves 5-Axis | manufacturing | K4 | unresolved | UG_MODELING_THROUGH_CURVES_FEATURE (0.26) |
| Rotary Milling | manufacturing | K4 | unresolved | — |
| Rotary Roughing | manufacturing | K3 | unresolved | — |
| Rotary Finishing | manufacturing | K3 | unresolved | — |
| Tool Axis Control | manufacturing | K4 | unresolved | UG_CAM_INFORMATION (0.33)<br>UG_CAM_CREATE_TOOL (0.30) |
| Tilt Tool Axis | manufacturing | K4 | unresolved | UG_CAM_VERIFY_TOOL_PATH (0.33)<br>UG_CAM_GENERATE_TOOL_PATH (0.31)<br>UG_CAM_CREATE_TOOL (0.30) |
| Lead/Lag | manufacturing | K3 | unresolved | — |
| Avoidance Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.45)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Collision Avoidance | manufacturing | K3 | unresolved | — |
| Automatic Tool Axis | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.33)<br>UG_CAM_GENERATE_TOOL_PATH (0.33)<br>UG_CAM_VERIFY_TOOL_PATH (0.28) |
| Blade Finish | manufacturing | K3 | unresolved | UG_PMI_SURFACE_FINISH (0.32) |
| Hub Finish | manufacturing | K3 | unresolved | UG_PMI_SURFACE_FINISH (0.30) |
| Fillet Finish | manufacturing | K3 | unresolved | UG_MODELING_BLEND_FEATURE (0.29)<br>UG_PMI_SURFACE_FINISH (0.29) |
| Blade Blend | manufacturing | K3 | unresolved | UG_MODELING_BLEND_FEATURE (0.46) |
| Multi-Axis Deburring | manufacturing | K3 | unresolved | — |
| Geodesic Milling | manufacturing | K4 | unresolved | — |
| Multi-Axis Additive | manufacturing | K3 | unresolved | — |
| Facing | manufacturing | K3 | unresolved | — |
| Rough Turn OD | manufacturing | K3 | unresolved | — |
| Finish Turn OD | manufacturing | K3 | unresolved | UG_PMI_SURFACE_FINISH (0.26) |
| Rough Bore | manufacturing | K3 | unresolved | — |
| Finish Bore | manufacturing | K3 | unresolved | UG_PMI_SURFACE_FINISH (0.30) |
| Back Turn | manufacturing | K3 | unresolved | — |
| Grooving OD | manufacturing | K3 | unresolved | — |
| Grooving ID | manufacturing | K3 | unresolved | — |
| Face Grooving | manufacturing | K3 | unresolved | UG_ANALYSIS_FACE_CURVATURE (0.37)<br>UG_SEL_FACE_PRIORITY (0.29) |
| Threading OD | manufacturing | K3 | unresolved | — |
| Threading ID | manufacturing | K3 | unresolved | — |
| Teach Mode | manufacturing | K3 | unresolved | — |
| Turn Contour | manufacturing | K3 | unresolved | UG_SHEET_METAL_CONTOUR_FLANGE (0.25) |
| Centerline Drilling | manufacturing | K4 | unresolved | — |
| Create Turning MCS | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.37)<br>UG_CAM_CREATE_OPERATION (0.34)<br>UG_ROUTE_CREATE_ROUTE (0.33) |
| Spindle Transfer | manufacturing | K3 | unresolved | — |
| Part Transfer | manufacturing | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.34)<br>UG_ROUTE_PLACE_PART (0.29) |
| Sync Manager | manufacturing | K3 | unresolved | UG_ASSY_WAVE_ASSOC_MANAGER (0.31)<br>UG_MATERIAL_LIBRARY_MANAGER (0.29) |
| Channel View | manufacturing | K4 | unresolved | UG_DRAFTING_BASE_VIEW (0.44)<br>UG_PMI_MODEL_VIEW (0.44)<br>UG_DRAFTING_DETAIL_VIEW (0.40) |
| B-Axis Turning | manufacturing | K4 | unresolved | — |
| Y-Axis Turning | manufacturing | K4 | unresolved | — |
| Polar Milling | manufacturing | K4 | unresolved | — |
| Cylindrical Milling | manufacturing | K4 | unresolved | — |
| 2-Axis Wire | manufacturing | K3 | unresolved | — |
| 4-Axis Wire | manufacturing | K3 | unresolved | — |
| Closed Profile | manufacturing | K3 | unresolved | — |
| Wire Thread Point | manufacturing | K3 | unresolved | — |
| No-Core Cutting | manufacturing | K3 | unresolved | — |
| Probe Geometry | manufacturing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Set Work Offset | manufacturing | K4 | unresolved | UG_SKETCH_OFFSET_CURVE (0.29) |
| Tool Setting | manufacturing | K4 | unresolved | UG_CAM_INFORMATION (0.34)<br>UG_CAM_CREATE_TOOL (0.30)<br>UG_CAM_VERIFY_TOOL_PATH (0.26) |
| Update Workpiece | manufacturing | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.43) |
| In-Process Inspection | manufacturing | K4 | unresolved | — |
| Machine Tool Builder | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.31)<br>UG_CAM_VERIFY_TOOL_PATH (0.30)<br>UG_CAM_GENERATE_TOOL_PATH (0.28) |
| Machine Kit Wizard | manufacturing | K3 | unresolved | UG_APP_MOLDWIZARD (0.33) |
| Kinematics Tree | manufacturing | K3 | unresolved | — |
| Create Axis | manufacturing | K4 | unresolved | UG_CAM_CREATE_TOOL (0.50)<br>UG_CAM_CREATE_OPERATION (0.46)<br>UG_SIM_CREATE_LOAD (0.46) |
| Define Junction | manufacturing | K3 | unresolved | — |
| Tool Mount | manufacturing | K4 | unresolved | UG_CAM_INFORMATION (0.32)<br>UG_CAM_VERIFY_TOOL_PATH (0.29)<br>UG_CAM_GENERATE_TOOL_PATH (0.27) |
| Part Mount | manufacturing | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.32)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.28) |
| Material Removal | manufacturing | K3 | unresolved | UG_MATERIAL_LIBRARY_MANAGER (0.35)<br>UG_MATERIAL_ASSIGN (0.33)<br>UG_DISPLAY_APPEARANCE_VISUAL_MATERIAL (0.33) |
| Collision Pair | manufacturing | K3 | unresolved | — |
| ISV | manufacturing | K3 | unresolved | — |
| AI Make Machining Suggestion | manufacturing | K3 | unresolved | — |
| Machine-Aware CAM Operations | manufacturing | K4 | unresolved | UG_CAM_CREATE_OPERATION (0.31)<br>UG_CAM_DELETE_OPERATION (0.30)<br>UG_CAM_INFORMATION (0.27) |
| Fixture Automation | manufacturing | K4 | unresolved | — |
| Integrated Work Holding Device Management | manufacturing | K3 | unresolved | — |
| Automatic Fixture Alignment | manufacturing | K4 | unresolved | — |
| AI-Powered Kinematics Tree Creation | manufacturing | K3 | unresolved | — |
| Create Build Setup | manufacturing | K3 | unresolved | UG_CAM_CREATE_TOOL (0.37)<br>UG_CAM_CREATE_OPERATION (0.34)<br>UG_ROUTE_CREATE_ROUTE (0.33) |
| Build Volume Check | manufacturing | K3 | unresolved | UG_SKETCH_CHECKER (0.47) |
| Overhang Analysis | manufacturing | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35) |
| Wall Thickness Check | manufacturing | K3 | unresolved | UG_SKETCH_CHECKER (0.46) |
| Generate Supports | manufacturing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.41) |
| Edit Supports | manufacturing | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_CUT (0.27) |
| Scan Path | manufacturing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.32)<br>UG_CAM_VERIFY_TOOL_PATH (0.31)<br>UG_CAM_INFORMATION (0.28) |
| 3D Deposition Path | manufacturing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.31)<br>UG_CAM_VERIFY_TOOL_PATH (0.31) |
| Post Process Build | manufacturing | K3 | unresolved | UG_CAM_POSTPROCESS (0.29) |
| Probe Assembly | manufacturing | K3 | unresolved | UG_APP_ASSEMBLIES (0.35)<br>UG_ASSEMBLIES_NAVIGATOR (0.26)<br>UG_ASSEMBLIES_CONSTRAINTS (0.26) |
| Part Setup | manufacturing | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.28) |
| Recognize Features | manufacturing | K3 | unresolved | — |
| Measure Line | manufacturing | K3 | ambiguous | UG_INFO_GEOMETRIC_MEASUREMENT (0.90)<br>UG_SKETCH_LINE (0.85) |
| Measure Circle | manufacturing | K3 | ambiguous | UG_SKETCH_CIRCLE (0.89)<br>UG_INFO_GEOMETRIC_MEASUREMENT (0.88) |
| Construct Feature | manufacturing | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.44)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.42)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.42) |
| Generate Inspection Path | manufacturing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.61)<br>UG_CAM_VERIFY_TOOL_PATH (0.33)<br>UG_CAM_INFORMATION (0.27) |
| Optimize Path | manufacturing | K3 | unresolved | UG_CAM_VERIFY_TOOL_PATH (0.34)<br>UG_CAM_GENERATE_TOOL_PATH (0.32)<br>UG_CAM_INFORMATION (0.30) |
| Simulate Inspection | manufacturing | K3 | unresolved | — |
| Post DMIS | manufacturing | K3 | unresolved | — |
| Inspection Report | manufacturing | K3 | unresolved | — |
| Edit Journal | manufacturing | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | manufacturing | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | manufacturing | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | manufacturing | K3 | unresolved | — |
| Close | simulation | K3 | unresolved | — |
| Close All | simulation | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | simulation | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | simulation | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | simulation | K3 | unresolved | — |
| Print | simulation | K3 | unresolved | — |
| Export | simulation | K4 | unresolved | — |
| Import | simulation | K4 | unresolved | — |
| Recently Opened Parts | simulation | K3 | unresolved | — |
| Switch Window | simulation | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | simulation | K3 | unresolved | — |
| Rename | simulation | K3 | unresolved | — |
| Object Properties | simulation | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | simulation | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | simulation | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | simulation | K3 | unresolved | — |
| Unsuppress | simulation | K3 | unresolved | — |
| Reorder | simulation | K3 | unresolved | — |
| Make Current Feature | simulation | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | simulation | K3 | unresolved | — |
| Selection Filter | simulation | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | simulation | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | simulation | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | simulation | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | simulation | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | simulation | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | simulation | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | simulation | K5 | unresolved | — |
| Pan | simulation | K5 | unresolved | — |
| Rotate | simulation | K5 | unresolved | — |
| Orient View | simulation | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | simulation | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | simulation | K3 | unresolved | — |
| Back | simulation | K3 | unresolved | — |
| Top | simulation | K3 | unresolved | — |
| Bottom | simulation | K3 | unresolved | — |
| Left | simulation | K3 | unresolved | — |
| Right | simulation | K3 | unresolved | — |
| Previous View | simulation | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | simulation | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | simulation | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | simulation | K3 | unresolved | — |
| Hide | simulation | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | simulation | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | simulation | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | simulation | K3 | unresolved | — |
| Shaded | simulation | K3 | unresolved | — |
| Shaded with Edges | simulation | K3 | unresolved | — |
| Examine Geometry | simulation | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | simulation | K3 | unresolved | — |
| Customize | simulation | K3 | unresolved | — |
| Roles | simulation | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | simulation | K3 | unresolved | — |
| Part Navigator | simulation | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Measure Results | simulation | K3 | ambiguous | UG_SIM_RESULTS (0.91)<br>UG_INFO_GEOMETRIC_MEASUREMENT (0.87) |
| Sequence Editor | simulation | K3 | unresolved | — |
| Rigid Body | simulation | K3 | unresolved | — |
| Collision Body | simulation | K3 | unresolved | UG_SEL_BODY_PRIORITY (0.29) |
| Transport Surface | simulation | K3 | unresolved | UG_MODELING_STUDIO_SURFACE_FEATURE (0.42)<br>UG_APP_MODELING (0.34) |
| Check In | simulation | K3 | unresolved | — |
| Check Out | simulation | K3 | unresolved | — |
| Cancel Check Out | simulation | K3 | unresolved | — |
| Impact Analysis | simulation | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | simulation | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | simulation | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | simulation | K3 | unresolved | — |
| Import Parasolid | simulation | K4 | unresolved | — |
| Import STEP | simulation | K4 | unresolved | — |
| Import IGES | simulation | K4 | unresolved | — |
| Import JT | simulation | K4 | unresolved | — |
| Import CATIA | simulation | K4 | unresolved | — |
| Import Creo | simulation | K4 | unresolved | — |
| Import SolidWorks | simulation | K4 | unresolved | — |
| Import DXF/DWG | simulation | K4 | unresolved | — |
| Import STL | simulation | K4 | unresolved | — |
| Import OBJ | simulation | K4 | unresolved | — |
| Import IFC | simulation | K4 | unresolved | — |
| Import XML | simulation | K4 | unresolved | — |
| Export Parasolid | simulation | K4 | unresolved | — |
| Export STEP AP203/214/242 | simulation | K4 | unresolved | — |
| Export IGES | simulation | K4 | unresolved | — |
| Export JT | simulation | K4 | unresolved | — |
| Export DXF/DWG | simulation | K4 | unresolved | — |
| Export STL | simulation | K4 | unresolved | — |
| Export 3MF | simulation | K4 | unresolved | — |
| Export PDF | simulation | K4 | unresolved | — |
| Export CGM | simulation | K4 | unresolved | — |
| Export QIF | simulation | K4 | unresolved | — |
| Publish Technical Data Package | simulation | K3 | unresolved | — |
| Heal Geometry | simulation | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | simulation | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | simulation | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | simulation | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | simulation | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | simulation | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | simulation | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | simulation | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | simulation | K3 | unresolved | — |
| Close | routing | K3 | unresolved | — |
| Close All | routing | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | routing | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | routing | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | routing | K3 | unresolved | — |
| Print | routing | K3 | unresolved | — |
| Export | routing | K4 | unresolved | — |
| Import | routing | K4 | unresolved | — |
| Recently Opened Parts | routing | K3 | unresolved | — |
| Switch Window | routing | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | routing | K3 | unresolved | — |
| Rename | routing | K3 | unresolved | — |
| Object Properties | routing | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | routing | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | routing | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | routing | K3 | unresolved | — |
| Unsuppress | routing | K3 | unresolved | — |
| Reorder | routing | K3 | unresolved | — |
| Make Current Feature | routing | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | routing | K3 | unresolved | — |
| Selection Filter | routing | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | routing | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | routing | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | routing | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | routing | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | routing | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | routing | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | routing | K5 | unresolved | — |
| Pan | routing | K5 | unresolved | — |
| Rotate | routing | K5 | unresolved | — |
| Orient View | routing | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | routing | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | routing | K3 | unresolved | — |
| Back | routing | K3 | unresolved | — |
| Top | routing | K3 | unresolved | — |
| Bottom | routing | K3 | unresolved | — |
| Left | routing | K3 | unresolved | — |
| Right | routing | K3 | unresolved | — |
| Previous View | routing | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | routing | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | routing | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | routing | K3 | unresolved | — |
| Hide | routing | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | routing | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | routing | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | routing | K3 | unresolved | — |
| Shaded | routing | K3 | unresolved | — |
| Shaded with Edges | routing | K3 | unresolved | — |
| Examine Geometry | routing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | routing | K3 | unresolved | — |
| Customize | routing | K3 | unresolved | — |
| Roles | routing | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | routing | K3 | unresolved | — |
| Part Navigator | routing | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Create Path | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.51)<br>UG_CAM_CREATE_TOOL (0.46)<br>UG_SIM_CREATE_LOAD (0.46) |
| Route Segment | routing | K3 | unresolved | UG_ROUTE_DELETE (0.38)<br>UG_ROUTE_CREATE_ROUTE (0.36)<br>UG_ROUTE_VALIDATE (0.35) |
| Spline Path | routing | K3 | unresolved | UG_CAM_VERIFY_TOOL_PATH (0.30)<br>UG_CAM_GENERATE_TOOL_PATH (0.29)<br>UG_ROUTE_PLACE_PART (0.26) |
| Offset Path | routing | K3 | unresolved | UG_SKETCH_OFFSET_CURVE (0.44)<br>UG_CAM_VERIFY_TOOL_PATH (0.33)<br>UG_CAM_GENERATE_TOOL_PATH (0.31) |
| Edit Path | routing | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.48)<br>UG_PMI_EDIT (0.47)<br>UG_EDIT_PASTE (0.40) |
| Heal Path | routing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.33)<br>UG_CAM_VERIFY_TOOL_PATH (0.33)<br>UG_CAM_INFORMATION (0.27) |
| Assign Stock | routing | K3 | unresolved | UG_ROUTE_ADD_STOCK (0.47)<br>UG_MATERIAL_ASSIGN (0.42) |
| Place Fitting | routing | K3 | unresolved | UG_ROUTE_PLACE_PART (0.45) |
| Replace Part | routing | K3 | unresolved | UG_ROUTE_PLACE_PART (0.58)<br>UG_ROUTE_REMOVE_PART (0.51)<br>UG_ASSEMBLIES_REPLACE_COMPONENT (0.44) |
| Route Clearance | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.40)<br>UG_ROUTE_EDIT_ROUTE (0.36)<br>UG_ROUTE_VALIDATE (0.35) |
| Route Length | routing | K3 | unresolved | UG_ROUTE_DELETE (0.40)<br>UG_ROUTE_VALIDATE (0.38)<br>UG_ROUTE_CREATE_ROUTE (0.37) |
| Generate Centerline | routing | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.34) |
| Flatten Route | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.49)<br>UG_ROUTE_VALIDATE (0.47)<br>UG_ROUTE_EDIT_ROUTE (0.45) |
| Create Connection | routing | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.49)<br>UG_SIM_CREATE_SOLUTION (0.49)<br>UG_SIM_CREATE_CONSTRAINT (0.47) |
| Assign Wire | routing | K3 | unresolved | UG_MATERIAL_ASSIGN (0.42) |
| Route Wire | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.40)<br>UG_ROUTE_EDIT_ROUTE (0.38)<br>UG_ROUTE_VALIDATE (0.38) |
| Route Cable | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.40)<br>UG_ROUTE_VALIDATE (0.38)<br>UG_ROUTE_DELETE (0.38) |
| Create Bundle | routing | K3 | unresolved | UG_ROUTE_CREATE_ROUTE (0.49)<br>UG_CAM_CREATE_TOOL (0.45)<br>UG_SIM_CREATE_LOAD (0.45) |
| Pin Assignment | routing | K3 | unresolved | — |
| Harness Flattening | routing | K3 | unresolved | — |
| Place Cable Trays | routing | K3 | unresolved | UG_ROUTE_PLACE_PART (0.38) |
| Automatic Tray Placement | routing | K3 | unresolved | — |
| Tray Fill Check | routing | K3 | unresolved | UG_SKETCH_CHECKER (0.39) |
| Check In | routing | K3 | unresolved | — |
| Check Out | routing | K3 | unresolved | — |
| Cancel Check Out | routing | K3 | unresolved | — |
| Impact Analysis | routing | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | routing | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | routing | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | routing | K3 | unresolved | — |
| Import Parasolid | routing | K4 | unresolved | — |
| Import STEP | routing | K4 | unresolved | — |
| Import IGES | routing | K4 | unresolved | — |
| Import JT | routing | K4 | unresolved | — |
| Import CATIA | routing | K4 | unresolved | — |
| Import Creo | routing | K4 | unresolved | — |
| Import SolidWorks | routing | K4 | unresolved | — |
| Import DXF/DWG | routing | K4 | unresolved | — |
| Import STL | routing | K4 | unresolved | — |
| Import OBJ | routing | K4 | unresolved | — |
| Import IFC | routing | K4 | unresolved | — |
| Import XML | routing | K4 | unresolved | — |
| Export Parasolid | routing | K4 | unresolved | — |
| Export STEP AP203/214/242 | routing | K4 | unresolved | — |
| Export IGES | routing | K4 | unresolved | — |
| Export JT | routing | K4 | unresolved | — |
| Export DXF/DWG | routing | K4 | unresolved | — |
| Export STL | routing | K4 | unresolved | — |
| Export 3MF | routing | K4 | unresolved | — |
| Export PDF | routing | K4 | unresolved | — |
| Export CGM | routing | K4 | unresolved | — |
| Export QIF | routing | K4 | unresolved | — |
| Publish Technical Data Package | routing | K3 | unresolved | — |
| Heal Geometry | routing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | routing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | routing | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | routing | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | routing | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | routing | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | routing | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | routing | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | routing | K3 | unresolved | — |
| Close | mold | K3 | unresolved | — |
| Close All | mold | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | mold | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | mold | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | mold | K3 | unresolved | — |
| Print | mold | K3 | unresolved | — |
| Export | mold | K4 | unresolved | — |
| Import | mold | K4 | unresolved | — |
| Recently Opened Parts | mold | K3 | unresolved | — |
| Switch Window | mold | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | mold | K3 | unresolved | — |
| Rename | mold | K3 | unresolved | — |
| Object Properties | mold | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | mold | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | mold | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | mold | K3 | unresolved | — |
| Unsuppress | mold | K3 | unresolved | — |
| Reorder | mold | K3 | unresolved | — |
| Make Current Feature | mold | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | mold | K3 | unresolved | — |
| Selection Filter | mold | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | mold | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | mold | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | mold | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | mold | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | mold | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | mold | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | mold | K5 | unresolved | — |
| Pan | mold | K5 | unresolved | — |
| Rotate | mold | K5 | unresolved | — |
| Orient View | mold | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | mold | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | mold | K3 | unresolved | — |
| Back | mold | K3 | unresolved | — |
| Top | mold | K3 | unresolved | — |
| Bottom | mold | K3 | unresolved | — |
| Left | mold | K3 | unresolved | — |
| Right | mold | K3 | unresolved | — |
| Previous View | mold | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | mold | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | mold | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | mold | K3 | unresolved | — |
| Hide | mold | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | mold | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | mold | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | mold | K3 | unresolved | — |
| Shaded | mold | K3 | unresolved | — |
| Shaded with Edges | mold | K3 | unresolved | — |
| Examine Geometry | mold | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | mold | K3 | unresolved | — |
| Customize | mold | K3 | unresolved | — |
| Roles | mold | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | mold | K3 | unresolved | — |
| Part Navigator | mold | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Define Workpiece | mold | K3 | unresolved | — |
| Draft Analysis | mold | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Region Analysis | mold | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.36) |
| Mold Drawing | mold | K3 | unresolved | UG_MOLD_MOLD_BASE (0.44)<br>UG_MOLD_COOLING (0.40)<br>UG_MOLD_PARTING (0.40) |
| Formability Analysis | mold | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.35) |
| Blank Generator | mold | K3 | unresolved | UG_EDIT_BLANK_SELECTED (0.27) |
| Bending | mold | K3 | unresolved | — |
| Die Drawing | mold | K3 | unresolved | — |
| Check In | mold | K3 | unresolved | — |
| Check Out | mold | K3 | unresolved | — |
| Cancel Check Out | mold | K3 | unresolved | — |
| Impact Analysis | mold | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | mold | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | mold | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | mold | K3 | unresolved | — |
| Import Parasolid | mold | K4 | unresolved | — |
| Import STEP | mold | K4 | unresolved | — |
| Import IGES | mold | K4 | unresolved | — |
| Import JT | mold | K4 | unresolved | — |
| Import CATIA | mold | K4 | unresolved | — |
| Import Creo | mold | K4 | unresolved | — |
| Import SolidWorks | mold | K4 | unresolved | — |
| Import DXF/DWG | mold | K4 | unresolved | — |
| Import STL | mold | K4 | unresolved | — |
| Import OBJ | mold | K4 | unresolved | — |
| Import IFC | mold | K4 | unresolved | — |
| Import XML | mold | K4 | unresolved | — |
| Export Parasolid | mold | K4 | unresolved | — |
| Export STEP AP203/214/242 | mold | K4 | unresolved | — |
| Export IGES | mold | K4 | unresolved | — |
| Export JT | mold | K4 | unresolved | — |
| Export DXF/DWG | mold | K4 | unresolved | — |
| Export STL | mold | K4 | unresolved | — |
| Export 3MF | mold | K4 | unresolved | — |
| Export PDF | mold | K4 | unresolved | — |
| Export CGM | mold | K4 | unresolved | — |
| Export QIF | mold | K4 | unresolved | — |
| Publish Technical Data Package | mold | K3 | unresolved | — |
| Heal Geometry | mold | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | mold | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | mold | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | mold | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | mold | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | mold | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | mold | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | mold | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | mold | K3 | unresolved | — |
| Close | reuse | K3 | unresolved | — |
| Close All | reuse | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | reuse | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | reuse | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | reuse | K3 | unresolved | — |
| Print | reuse | K3 | unresolved | — |
| Export | reuse | K4 | unresolved | — |
| Import | reuse | K4 | unresolved | — |
| Recently Opened Parts | reuse | K3 | unresolved | — |
| Switch Window | reuse | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | reuse | K3 | unresolved | — |
| Rename | reuse | K3 | unresolved | — |
| Object Properties | reuse | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | reuse | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | reuse | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | reuse | K3 | unresolved | — |
| Unsuppress | reuse | K3 | unresolved | — |
| Reorder | reuse | K3 | unresolved | — |
| Make Current Feature | reuse | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | reuse | K3 | unresolved | — |
| Selection Filter | reuse | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | reuse | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | reuse | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | reuse | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | reuse | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | reuse | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | reuse | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | reuse | K5 | unresolved | — |
| Pan | reuse | K5 | unresolved | — |
| Rotate | reuse | K5 | unresolved | — |
| Orient View | reuse | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | reuse | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | reuse | K3 | unresolved | — |
| Back | reuse | K3 | unresolved | — |
| Top | reuse | K3 | unresolved | — |
| Bottom | reuse | K3 | unresolved | — |
| Left | reuse | K3 | unresolved | — |
| Right | reuse | K3 | unresolved | — |
| Previous View | reuse | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | reuse | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | reuse | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | reuse | K3 | unresolved | — |
| Hide | reuse | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | reuse | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | reuse | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | reuse | K3 | unresolved | — |
| Shaded | reuse | K3 | unresolved | — |
| Shaded with Edges | reuse | K3 | unresolved | — |
| Examine Geometry | reuse | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | reuse | K3 | unresolved | — |
| Customize | reuse | K3 | unresolved | — |
| Roles | reuse | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | reuse | K3 | unresolved | — |
| Check In | reuse | K3 | unresolved | — |
| Check Out | reuse | K3 | unresolved | — |
| Cancel Check Out | reuse | K3 | unresolved | — |
| Impact Analysis | reuse | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | reuse | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | reuse | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | reuse | K3 | unresolved | — |
| Import Parasolid | reuse | K4 | unresolved | — |
| Import STEP | reuse | K4 | unresolved | — |
| Import IGES | reuse | K4 | unresolved | — |
| Import JT | reuse | K4 | unresolved | — |
| Import CATIA | reuse | K4 | unresolved | — |
| Import Creo | reuse | K4 | unresolved | — |
| Import SolidWorks | reuse | K4 | unresolved | — |
| Import DXF/DWG | reuse | K4 | unresolved | — |
| Import STL | reuse | K4 | unresolved | — |
| Import OBJ | reuse | K4 | unresolved | — |
| Import IFC | reuse | K4 | unresolved | — |
| Import XML | reuse | K4 | unresolved | — |
| Export Parasolid | reuse | K4 | unresolved | — |
| Export STEP AP203/214/242 | reuse | K4 | unresolved | — |
| Export IGES | reuse | K4 | unresolved | — |
| Export JT | reuse | K4 | unresolved | — |
| Export DXF/DWG | reuse | K4 | unresolved | — |
| Export STL | reuse | K4 | unresolved | — |
| Export 3MF | reuse | K4 | unresolved | — |
| Export PDF | reuse | K4 | unresolved | — |
| Export CGM | reuse | K4 | unresolved | — |
| Export QIF | reuse | K4 | unresolved | — |
| Publish Technical Data Package | reuse | K3 | unresolved | — |
| Heal Geometry | reuse | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | reuse | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | reuse | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | reuse | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | reuse | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | reuse | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | reuse | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | reuse | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | reuse | K3 | unresolved | — |
| Close | inspect_view | K3 | unresolved | — |
| Close All | inspect_view | K3 | unresolved | UG_SEL_SELECT_ALL (0.40)<br>UG_SEL_DESELECT_ALL (0.37) |
| Reopen | inspect_view | K3 | unresolved | UG_FILE_OPEN (0.28) |
| Part Cleanup | inspect_view | K3 | unresolved | UG_NAVIGATOR_PART (0.37)<br>UG_ROUTE_PLACE_PART (0.30)<br>UG_VIEW_PALETTE_MATERIALS_IN_PART (0.30) |
| Properties | inspect_view | K3 | unresolved | — |
| Print | inspect_view | K3 | unresolved | — |
| Export | inspect_view | K4 | unresolved | — |
| Import | inspect_view | K4 | unresolved | — |
| Recently Opened Parts | inspect_view | K3 | unresolved | — |
| Switch Window | inspect_view | K3 | unresolved | UG_APP_PMI (0.37)<br>UG_APP_ROUTING (0.34)<br>UG_APP_DRAFTING (0.33) |
| Exit | inspect_view | K3 | unresolved | — |
| Rename | inspect_view | K3 | unresolved | — |
| Object Properties | inspect_view | K3 | unresolved | UG_INFO_OBJECT (0.43)<br>UG_ROUTE_DELETE (0.26) |
| Edit Parameters | inspect_view | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.42)<br>UG_PMI_EDIT (0.39)<br>UG_EDIT_PASTE (0.31) |
| Edit with Rollback | inspect_view | K4 | unresolved | UG_ROUTE_EDIT_ROUTE (0.31)<br>UG_PMI_EDIT (0.26) |
| Suppress | inspect_view | K3 | unresolved | — |
| Unsuppress | inspect_view | K3 | unresolved | — |
| Reorder | inspect_view | K3 | unresolved | — |
| Make Current Feature | inspect_view | K4 | unresolved | UG_MODELING_MIRRORFEATURE_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.38)<br>UG_MODELING_SHEET_FEATURE (0.38) |
| Select Similar Faces/Edges | inspect_view | K3 | unresolved | — |
| Selection Filter | inspect_view | K5 | ambiguous | UG_SEL_TYPE_RESET (0.93)<br>UG_APP_GATEWAY (0.89)<br>UG_SEL_BODY_PRIORITY (0.35) |
| Select Connected | inspect_view | K3 | unresolved | UG_SEL_SELECT_ALL (0.38) |
| Select Tangent Faces | inspect_view | K3 | unresolved | UG_SEL_SELECT_ALL (0.31)<br>UG_SKETCH_TANGENT_CONSTRAINT (0.28) |
| Select Feature | inspect_view | K4 | unresolved | UG_MODELING_SHEET_FEATURE (0.52)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.45)<br>UG_MODELING_MIRRORFEATURE_FEATURE (0.43) |
| Select Body | inspect_view | K4 | unresolved | UG_SEL_SELECT_ALL (0.46)<br>UG_SEL_BODY_PRIORITY (0.31)<br>UG_APP_GATEWAY (0.27) |
| Select Component | inspect_view | K4 | unresolved | UG_ASSEMBLIES_REPLACE_COMPONENT (0.51)<br>UG_ASSEMBLIES_MOVE_COMPONENT (0.48)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.48) |
| QuickPick | inspect_view | K5 | unresolved | UG_SKETCH_RAPID_DIMENSION (0.46) |
| Zoom In/Out | inspect_view | K5 | unresolved | — |
| Pan | inspect_view | K5 | unresolved | — |
| Rotate | inspect_view | K5 | unresolved | — |
| Orient View | inspect_view | K5 | unresolved | UG_DRAFTING_PROJECTED_VIEW (0.43)<br>UG_DRAFTING_BASE_VIEW (0.42)<br>UG_PMI_MODEL_VIEW (0.42) |
| Isometric | inspect_view | K5 | unresolved | UG_VIEW_POPUP_ORIENT_TFRTRI (0.28) |
| Front | inspect_view | K3 | unresolved | — |
| Back | inspect_view | K3 | unresolved | — |
| Top | inspect_view | K3 | unresolved | — |
| Bottom | inspect_view | K3 | unresolved | — |
| Left | inspect_view | K3 | unresolved | — |
| Right | inspect_view | K3 | unresolved | — |
| Previous View | inspect_view | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.42)<br>UG_DRAFTING_PROJECTED_VIEW (0.40)<br>UG_DRAFTING_DETAIL_VIEW (0.39) |
| Named Views | inspect_view | K4 | unresolved | UG_DRAFTING_UPDATE_VIEWS (0.44)<br>UG_DRAFTING_BASE_VIEW (0.27) |
| Clip Section | inspect_view | K4 | unresolved | UG_DRAFTING_SECTION_VIEW (0.27) |
| Perspective | inspect_view | K3 | unresolved | — |
| Hide | inspect_view | K5 | ambiguous | UG_EDIT_BLANK_SELECTED (0.84)<br>UG_EDIT_MD_SHOWHIDE_ALL (0.84) |
| Show Only | inspect_view | K5 | unresolved | UG_EDIT_MD_SHOWHIDE_ALL (0.34) |
| Unblank | inspect_view | K3 | unresolved | UG_SKETCH_TRIM (0.25) |
| Wireframe | inspect_view | K3 | unresolved | — |
| Shaded | inspect_view | K3 | unresolved | — |
| Shaded with Edges | inspect_view | K3 | unresolved | — |
| Examine Geometry | inspect_view | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.48)<br>UG_ASSY_WAVE_LINKER (0.31)<br>UG_MODELING_WAVE_LINKER (0.31) |
| Shortcut Keys | inspect_view | K3 | unresolved | — |
| Customize | inspect_view | K3 | unresolved | — |
| Roles | inspect_view | K3 | unresolved | UG_MODELING_HOLE_FEATURE (0.25) |
| Resource Bar | inspect_view | K3 | unresolved | — |
| Part Navigator | inspect_view | K5 | ambiguous | UG_NAVIGATOR_PART (1.00)<br>UG_ASSY_WAVE_PART_NAVIGATOR (0.93)<br>UG_CAM_OPERATION_NAVIGATOR (0.48) |
| Geometry Checker | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.46)<br>UG_ASSY_WAVE_LINKER (0.38)<br>UG_MODELING_WAVE_LINKER (0.38) |
| Face and Edge Check | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.30)<br>UG_ANALYSIS_FACE_CURVATURE (0.28) |
| Body Consistency | inspect_view | K3 | unresolved | UG_SEL_BODY_PRIORITY (0.31) |
| Draft Analysis | inspect_view | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.36) |
| Undercut Analysis | inspect_view | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.38) |
| Moldability Check | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.30) |
| Sharp Edge Check | inspect_view | K3 | unresolved | UG_MODELING_BLEND_FEATURE (0.30) |
| Check-Mate | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.38) |
| Milling Checker | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.42) |
| Assembly Checker | inspect_view | K3 | unresolved | UG_SKETCH_CHECKER (0.46)<br>UG_ASSEMBLIES_NAVIGATOR (0.43)<br>UG_ASSEMBLIES_CONSTRAINTS (0.40) |
| Compare Body | inspect_view | K3 | unresolved | — |
| Edit Material | inspect_view | K3 | unresolved | UG_MATERIAL_ASSIGN (0.47)<br>UG_PMI_EDIT (0.39)<br>UG_ROUTE_EDIT_ROUTE (0.39) |
| Export Image | inspect_view | K3 | unresolved | — |
| Create Timeline | inspect_view | K3 | unresolved | UG_CAM_CREATE_TOOL (0.45)<br>UG_ROUTE_CREATE_ROUTE (0.42)<br>UG_SIM_CREATE_LOAD (0.42) |
| Camera Path | inspect_view | K3 | unresolved | UG_CAM_GENERATE_TOOL_PATH (0.33)<br>UG_CAM_VERIFY_TOOL_PATH (0.28)<br>UG_CAM_INFORMATION (0.26) |
| Export Movie | inspect_view | K3 | unresolved | — |
| Check In | inspect_view | K3 | unresolved | — |
| Check Out | inspect_view | K3 | unresolved | — |
| Cancel Check Out | inspect_view | K3 | unresolved | — |
| Impact Analysis | inspect_view | K3 | unresolved | UG_INFO_ANALYSIS_SHEET_BOUNDARY (0.33) |
| Assign Project | inspect_view | K3 | unresolved | UG_MOLD_INITIALIZE_PROJECT (0.40)<br>UG_MATERIAL_ASSIGN (0.39) |
| Create Live Share Session | inspect_view | K3 | unresolved | UG_CAM_CREATE_OPERATION (0.32)<br>UG_SIM_CREATE_SOLUTION (0.30)<br>UG_SIM_CREATE_CONSTRAINT (0.28) |
| Task Assignment | inspect_view | K3 | unresolved | — |
| Import Parasolid | inspect_view | K4 | unresolved | — |
| Import STEP | inspect_view | K4 | unresolved | — |
| Import IGES | inspect_view | K4 | unresolved | — |
| Import JT | inspect_view | K4 | unresolved | — |
| Import CATIA | inspect_view | K4 | unresolved | — |
| Import Creo | inspect_view | K4 | unresolved | — |
| Import SolidWorks | inspect_view | K4 | unresolved | — |
| Import DXF/DWG | inspect_view | K4 | unresolved | — |
| Import STL | inspect_view | K4 | unresolved | — |
| Import OBJ | inspect_view | K4 | unresolved | — |
| Import IFC | inspect_view | K4 | unresolved | — |
| Import XML | inspect_view | K4 | unresolved | — |
| Export Parasolid | inspect_view | K4 | unresolved | — |
| Export STEP AP203/214/242 | inspect_view | K4 | unresolved | — |
| Export IGES | inspect_view | K4 | unresolved | — |
| Export JT | inspect_view | K4 | unresolved | — |
| Export DXF/DWG | inspect_view | K4 | unresolved | — |
| Export STL | inspect_view | K4 | unresolved | — |
| Export 3MF | inspect_view | K4 | unresolved | — |
| Export PDF | inspect_view | K4 | unresolved | — |
| Export CGM | inspect_view | K4 | unresolved | — |
| Export QIF | inspect_view | K4 | unresolved | — |
| Publish Technical Data Package | inspect_view | K3 | unresolved | — |
| Heal Geometry | inspect_view | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.46)<br>UG_ASSY_WAVE_LINKER (0.33)<br>UG_MODELING_WAVE_LINKER (0.33) |
| Optimize Geometry | inspect_view | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.44)<br>UG_ASSY_WAVE_LINKER (0.27)<br>UG_MODELING_WAVE_LINKER (0.27) |
| Remove Parameters | inspect_view | K3 | unresolved | UG_ROUTE_REMOVE_PART (0.47)<br>UG_ASSEMBLIES_REMOVE_COMPONENT (0.39) |
| Feature Recognition | inspect_view | K4 | unresolved | UG_SEL_FEATURE_PRIORITY (0.35)<br>UG_PMI_FEATURE_CONTROL_FRAME (0.32)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.30) |
| Compare Imported Geometry | inspect_view | K4 | unresolved | UG_MODELING_EXTRACT_GEOMETRY (0.33) |
| Edit Journal | inspect_view | K3 | unresolved | UG_ROUTE_EDIT_ROUTE (0.44)<br>UG_PMI_EDIT (0.37)<br>UG_EDIT_UNDO (0.29) |
| User Defined Feature | inspect_view | K3 | unresolved | UG_MODELING_SHEET_FEATURE (0.38)<br>UG_MODELING_PATTERNFEATURE_FEATURE (0.35)<br>UG_MODELING_SEW_FEATURE (0.35) |
| Export Command List | inspect_view | K3 | unresolved | UG_DRAFTING_PARTS_LIST (0.32)<br>UG_HELP_COMMAND_FINDER (0.30) |
| Drawing Standards | inspect_view | K3 | unresolved | — |
