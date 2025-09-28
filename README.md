# Platter
Platter is a Cities: Skylines 2 mod that allows placing Parcels with vanilla zoning blocks. The goal is to create a tool that sits somewhere between vanilla and building plopping, giving players control over the zoning grid,
while still leveraging the vanilla zoning tools and building spawning/growth.

## Dependencies
`Dependencies here`

## Donations
`donations link here`

## Translations
`Translations link here`

## Support
I will respond on the code modding channels on **Cities: Skylines Modding Discord**

## System Overview for Maintainers
Overview of the various systems and what they do:

### PlatterPrefabSystem 
- SystemUpdatePhase: UIUpdate
- Description: Creates all custom prefabs on game load

### ParcelInitializeSystem 
- SystemUpdatePhase: PrefabUpdate
- Description: Initializes parcel prefabs. Runs after ObjectInitializeSystem and manually sets physical data.

### ParcelCreateSystem 
- SystemUpdatePhase: Modification3
- Description: Runs when a parcel is created. Mostly used for adding data the Tool needs.

### ParcelSpawnSystem 
- SystemUpdatePhase: Modification3
- Description: Controls "Spawnable" component on parcels and updates it.

### VanillaRoadInitializeSystem 
- SystemUpdatePhase: Modification4
- Description: Runs whenever a road is created to attach custom components.

### ParcelUpdateSystem 
- SystemUpdatePhase: Modification4
- Description: Runs when a parcel is updated. Handles a lot of the block and placement logic.

### RoadConnectionSystem 
- SystemUpdatePhase: Modification4B
- Description: Complex system that handles Parcel <-> Road connections.

### ParcelToBlockReferenceSystem 
- SystemUpdatePhase: Modification5
- Description: Handles ownership related components between Blocks and Parcels

### ParcelBlockToRoadReferenceSystem 
- SystemUpdatePhase: Modification5
- Description: Handles ownership related components between Blocks and Roads

### PlatterUISystem 
- SystemUpdatePhase: UIUpdate
- Description: Handles Mod UI and Keyboard bindings

### SelectedInfoPanelSystem 
- SystemUpdatePhase: UIUpdate
- Description: Handles Info Panel data. Currently not used.

### PlatterOverlaySystem 
- SystemUpdatePhase: Rendering
- Description: Handles Overlay / Parcel rendering

### PlatterTooltipSystem 
- SystemUpdatePhase: UITooltip
- Description: Handles Tooltips. 


## Credits 
* luca - Mod Author
* yenyang