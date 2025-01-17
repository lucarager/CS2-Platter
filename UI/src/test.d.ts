export type TransitionGroupCoordinator = (props: {
    skipInitial?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type DOMNodeProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PortalContainerProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Portal = (props: {
    children:string | JSX.Element | JSX.Element[];
container:any;
}) => JSX.Element;
export type ClassNameTransition = (props: {
    styles:any;
enterDuration:any;
exitDuration:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusNode = (props: {
    controller:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusBoundary = (props: {
    disabled:boolean;
children:string | JSX.Element | JSX.Element[];
onFocusChange:function;
}) => JSX.Element;
export type TutorialTargetProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Tooltip = (props: {
    tooltip:string | JSX.Element | null;
forceVisible:any;
disabled:boolean;
theme?:number;
direction:any;
alignment:any;
className:string;
children:string | JSX.Element | JSX.Element[];
anchorElRef:any;
}) => JSX.Element;
export type NavigationScope = (props: {
    focusKey?:number;
debugName?:number;
focused:any;
direction?:number;
activation?:number;
limits?:number;
children:string | JSX.Element | JSX.Element[];
onChange:function;
onRefocus:function;
allowFocusExit?:number;
allowLooping?:number;
jumpSections?:number;
}) => JSX.Element;
export type AutoNavigationScope = (props: {
    focusKey?:number;
initialFocused:any;
direction:any;
activation:any;
limits:any;
children:string | JSX.Element | JSX.Element[];
onChange:function;
onRefocus?:number;
allowFocusExit?:number;
forceFocus:any;
debugName?:number;
allowLooping?:number;
jumpSections:any;
}) => JSX.Element;
export type FocusKeyOverride = (props: {
    focusKey?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusScope = (props: {
    focusKey?:number;
debugName?:number;
focused:any;
activation?:number;
limits?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusDisabled = (props: {
    disabled?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusRoot = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type SelectableFocusBoundary = (props: {
    onSelectedStateChanged:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PanelBackdrop = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
onMouseDown:function;
}) => JSX.Element;
export type PointerBarrier = (props: {
    onClick:function;
}) => JSX.Element;
export type InputStackDebugDisplay = (props: {
    actions:any;
}) => JSX.Element;
export type EventInputProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type GamepadPointerEventProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type NativeInputProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TriggerItem = (props: {
    trigger:any;
}) => JSX.Element;
export type TutorialLayout = (props: {
    tutorial:any;
phase:any;
pagesVisible:any;
theme:any;
localization:any;
closeable?:number;
scrollable?:number;
disableTooltips?:number;
className:string;
closeHint:any;
}) => JSX.Element;
export type TutorialPhaseLayoutImpl = (props: {
    theme?:number;
className:string;
image:any;
icon:any;
title:any;
autoScroll?:number;
description:any;
onClose:function;
scrollable:any;
trigger:any;
pagesVisible:any;
phaseIndex?:number;
phaseCount?:number;
nextVisible:any;
previousVisible:any;
onActivatePreviousPhase:function;
onActivateNextPhase:function;
animateButton:any;
disableTooltips:any;
isCenterCard:any;
closeHint:any;
}) => JSX.Element;
export type TutorialBalloon = (props: {
    tutorial:any;
phase:any;
localization:any;
direction:any;
alignment:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TutorialTarget = (props: {
    uiTag:string;
active?:number;
disableBlinking?:number;
editor?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ControlIcons = (props: {
    modifiers:any;
bindings:any;
showName:any;
shortName:any;
theme:any;
className:string;
style:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FloatingInputHint = (props: {
    action:any;
active:any;
className:string;
}) => JSX.Element;
export type Dialog = (props: {
    wide:any;
title:any;
content:any;
buttons:any;
theme?:number;
zIndex:any;
onClose:function;
initialFocus:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type DialogRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ScrollActionConsumer = (props: {
    controller:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ParadoxDialog = (props: {
    wide:any;
onClose:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type MessageDialog = (props: {
    icon:any;
titleId:any;
messageId:any;
messageArgs:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FormField = (props: {
    type:any;
value:any;
placeholder:any;
invalid:any;
className:string;
vkTitle:any;
children:string | JSX.Element | JSX.Element[];
onChange:function;
}) => JSX.Element;
export type LabeledToggle = (props: {
    value:any;
label:any;
invalid:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type AnchoredPopup = (props: {
    visible:any;
direction?:number;
alignment?:number;
minHeight?:number;
minWidth?:number;
className:string;
content:any;
children:string | JSX.Element | JSX.Element[];
style:any;
}) => JSX.Element;
export type Dropdown = (props: {
    focusKey:UniqueFocusKey | null;
initialFocused:any;
theme?:number;
content:any;
alignment:any;
children:string | JSX.Element | JSX.Element[];
onToggle:function;
}) => JSX.Element;
export type DragHandle = (props: {
    onDrag:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InfoSection = (props: {
    focusKey:UniqueFocusKey | null;
tooltip:string | JSX.Element | null;
disableFocus?:number;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Divider = (props: {
    noMargin?:number;
}) => JSX.Element;
export type HeightTransition = (props: {
    styles:any;
enterDuration:any;
exitDuration:any;
enterExpandHeight?:number;
exitCollapseHeight?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TransitionCoordinator = (props: {
    in:any;
skipInitial?:number;
onEnter:function;
onExit:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FoldoutItem = (props: {
    header:any;
theme?:number;
type?:number;
nesting?:number;
initialExpanded?:number;
expandFromContent?:number;
onSelect:(x: any) => any;
onToggleExpanded:function;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FoldoutItemHeader = (props: {
    onClick:function;
onFocusChange:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InfoSectionFoldout = (props: {
    header:any;
initialExpanded:any;
expandFromContent:any;
focusKey:UniqueFocusKey | null;
tooltip:string | JSX.Element | null;
disableFocus?:number;
className:string;
onToggleExpanded:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InfoRow = (props: {
    icon:any;
left:any;
right:any;
tooltip:string | JSX.Element | null;
link:any;
uppercase?:number;
subRow?:number;
disableFocus?:number;
className:string;
}) => JSX.Element;
export type TooltipRow = (props: {
    icon:any;
left:any;
right:any;
uppercase?:number;
subRow?:number;
}) => JSX.Element;
export type Icon = (props: {
    tinted:any;
className:string;
src:string;
}) => JSX.Element;
export type Portal = (props: {
    children:string | JSX.Element | JSX.Element[];
container:any;
}) => JSX.Element;
export type ErrorDialog = (props: {
    severity?:number;
title:any;
message:any;
errorDetails:any;
canQuit?:number;
canSaveAndQuit?:number;
canSaveAndContinue?:number;
}) => JSX.Element;
export type ErrorDialogRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BoundDebugButton = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type BoundContainer = (props: {
    parent:any;
path:any;
children:string | JSX.Element | JSX.Element[];
props:any;
}) => JSX.Element;
export type Container = (props: {
    title:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ArrowControl = (props: {
    onSelectLeft:function;
onSelectRight:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FieldLayout = (props: {
    label:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type IntArrowField = (props: {
    label:any;
value:any;
min:any;
max:any;
step?:number;
stepMultiplier?:number;
onChange:function;
}) => JSX.Element;
export type FloatArrowField = (props: {
    label:any;
value:any;
min:any;
max:any;
step?:number;
stepMultiplier?:number;
fractionDigits?:number;
onChange:function;
}) => JSX.Element;
export type BoundFoldout = (props: {
    parent:any;
path:any;
children:string | JSX.Element | JSX.Element[];
props:any;
}) => JSX.Element;
export type Foldout = (props: {
    title:any;
value:any;
opened:any;
children:string | JSX.Element | JSX.Element[];
onOpenedChange:function;
}) => JSX.Element;
export type StatefulFoldout = (props: {
    title:any;
value:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type IntSliderField = (props: {
    label:any;
value:any;
min:any;
max:any;
step?:number;
onChange:function;
}) => JSX.Element;
export type FloatSliderField = (props: {
    label:any;
value:any;
min:any;
max:any;
step:any;
fractionDigits:any;
onChange:function;
}) => JSX.Element;
export type Float2SliderField = (props: {
    label:any;
value:any;
step:any;
fractionDigits:any;
min:any;
max:any;
onChange:function;
}) => JSX.Element;
export type Float3SliderField = (props: {
    label:any;
value:any;
step:any;
fractionDigits:any;
min:any;
max:any;
onChange:function;
}) => JSX.Element;
export type Float4SliderField = (props: {
    label:any;
value:any;
step:any;
fractionDigits:any;
min:any;
max:any;
onChange:function;
}) => JSX.Element;
export type ColorField = (props: {
    label:any;
value:any;
showAlpha:any;
onChange:function;
}) => JSX.Element;
export type ToggleField = (props: {
    label:any;
value:any;
onChange:function;
}) => JSX.Element;
export type EnumField = (props: {
    label:any;
value:any;
enumMembers:any;
onChange:function;
}) => JSX.Element;
export type BitField = (props: {
    label:any;
value:any;
enumMembers:any;
onChange:function;
}) => JSX.Element;
export type StringInputField = (props: {
    label:any;
value:any;
onChange:function;
}) => JSX.Element;
export type IntInputField = (props: {
    label:any;
value:any;
onChange:function;
}) => JSX.Element;
export type BoundValueField = (props: {
    props:any;
}) => JSX.Element;
export type BoundImage = (props: {
    props:any;
}) => JSX.Element;
export type DebugWidgetListRenderer = (props: {
    parent:any;
data:any;
}) => JSX.Element;
export type DebugBindingOutput = (props: {
    binding:any;
className:string;
}) => JSX.Element;
export type ColorLegend = (props: {
    color:any;
label:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ColorLegendSymbol = (props: {
    color:any;
className:string;
}) => JSX.Element;
export type WatchesOutput = (props: {
    watches:any;
className:string;
}) => JSX.Element;
export type HistoryChart = (props: {
    watches:any;
}) => JSX.Element;
export type DistributionChart = (props: {
    watches:any;
}) => JSX.Element;
export type DebugUIRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type DebugUI = (props: {
    focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type ModdingBetaElement = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BetaElement = (props: {
    children:string | JSX.Element | JSX.Element[];
binding:any;
}) => JSX.Element;
export type EditorItem = (props: {
    disabled:boolean;
centered:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FocusableEditorItem = (props: {
    disabled:boolean;
centered:any;
className:string;
focusKey?:number;
onFocusChange:function;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ExpandableEditorItem = (props: {
    label:any;
summary:any;
expanded:any;
disabled:boolean;
centered:any;
className:string;
tooltip:string | JSX.Element | null;
children:string | JSX.Element | JSX.Element[];
onExpandedChange:function;
}) => JSX.Element;
export type EditorItemControl = (props: {
    label:any;
onSelect:(x: any) => any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type EditorItemDetails = (props: {
    visible:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BoundAssetListField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type AssetListField = (props: {
    data:any;
onRemove:function;
}) => JSX.Element;
export type AssetListItem = (props: {
    item:any;
index:any;
onRemove:function;
}) => JSX.Element;
export type BoundEditorStringInputField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type StringInputField = (props: {
    value:any;
disabled:boolean;
onChange:function;
onChangeStart:function;
onChangeEnd:function;
className:string;
multiline:any;
maxLength:any;
}) => JSX.Element;
export type BoundExternalLinkField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type ExternalLinkField = (props: {
    data:any;
onAdd:function;
onChange:function;
onRemove:function;
}) => JSX.Element;
export type ExternalLinkItem = (props: {
    index:any;
link:any;
items:any;
onChange:function;
onRemove:function;
}) => JSX.Element;
export type BoundErrorLabel = (props: {
    props:any;
}) => JSX.Element;
export type BoundStringInputFieldWithError = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type BoundLargeIconButton = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type BoundLabel = (props: {
    props:any;
}) => JSX.Element;
export type ProgressCircle = (props: {
    progress:any;
max:any;
indeterminate:any;
theme:any;
className:string;
}) => JSX.Element;
export type BoundProgressIndicator = (props: {
    props:any;
}) => JSX.Element;
export type ProgressIndicator = (props: {
    progress:any;
indeterminate:any;
state:any;
hidden:any;
}) => JSX.Element;
export type BoundColumn = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Column = (props: {
    className:string;
style:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BoundCompass = (props: {
    props:any;
}) => JSX.Element;
export type Compass = (props: {
    angle:any;
className:string;
}) => JSX.Element;
export type SVGParentContextProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type GridLines = (props: {
    halfSteps:any;
overdraw?:number;
drawAxes?:number;
tickWidth?:number;
tickHeight?:number;
}) => JSX.Element;
export type FloatingMouseTooltip = (props: {
    position:any;
tooltip:string | JSX.Element | null;
disabled:boolean;
theme:any;
alwaysVisible?:number;
forceVisible?:number;
screenSpacePosition?:number;
style:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type AnimationCurveDeviation = (props: {
    keys?:number;
base:any;
color:any;
className:string;
}) => JSX.Element;
export type AnimationCurvePath = (props: {
    keys?:number;
color:any;
className:string;
}) => JSX.Element;
export type KeyframeTooltip = (props: {
    keyframe:any;
hide:any;
}) => JSX.Element;
export type KeyframeControlPoint = (props: {
    keyframe:any;
index:any;
pos:any;
type:any;
isFocused:any;
}) => JSX.Element;
export type KeyframeControlPoints = (props: {
    index:any;
data:any;
keyframes:any;
editTarget:any;
curve:any;
}) => JSX.Element;
export type AnimationCurveEditor = (props: {
    children:string | JSX.Element | JSX.Element[];
style:any;
zoomable:any;
panable:any;
padding:any;
className:string;
gridLines?:number;
drawPaths?:number;
showTooltipOnDrag?:number;
tooltipKeyframe:any;
textScale?:number;
formatTooltip?:number;
formatLabelX?:number;
formatLabelY?:number;
onClick:function;
onMouseDown:function;
onWindowMouseMove:function;
keyframeMoveCallback:any;
zoomCallback:any;
panCallback:any;
OnFocusChange:any;
focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type AnimationCurvePreview = (props: {
    data:any;
onSelect:(x: any) => any;
className:string;
}) => JSX.Element;
export type BoundGroup = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BoundExpandableGroup = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type EditorGroup = (props: {
    label:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ExpandableEditorGroup = (props: {
    label:any;
expanded:any;
tooltip:string | JSX.Element | null;
children:string | JSX.Element | JSX.Element[];
onExpandedChange:function;
}) => JSX.Element;
export type AnimationCurveField = (props: {
    label:any;
value:any;
disabled:boolean;
hidePreview:any;
expanded:any;
initialEditing:any;
autoUpdateBounds:any;
bounds:any;
smoothenOnMove:any;
canAddKeyframes?:number;
wrapMode:any;
parseKeyframe:any;
onAddKeyframe:function;
onMoveKeyframe:function;
onRemoveKeyframe:function;
onSetKeyframes:function;
}) => JSX.Element;
export type IntSliderField = (props: {
    label:any;
value:any;
min:any;
max:any;
disabled:boolean;
tooltip:string | JSX.Element | null;
onChange:function;
onChangeStart:function;
onChangeEnd:function;
}) => JSX.Element;
export type TimeSliderField = (props: {
    label:any;
value:any;
min:any;
max:any;
disabled:boolean;
tooltip:string | JSX.Element | null;
onChange:function;
onChangeStart:function;
onChangeEnd:function;
}) => JSX.Element;
export type ComponentInput = (props: {
    label:any;
value:any;
scale:any;
step:any;
gradient:any;
alpha:any;
onChange:function;
textField?:number;
}) => JSX.Element;
export type HsvComponentInputs = (props: {
    color:any;
onChange:function;
textField?:number;
}) => JSX.Element;
export type RgbComponentInputs = (props: {
    color:any;
onChange:function;
scale?:number;
step?:number;
textField?:number;
}) => JSX.Element;
export type AlphaComponentInput = (props: {
    color:any;
scale?:number;
step?:number;
onChange:function;
textField?:number;
}) => JSX.Element;
export type RadialHueWheel = (props: {
    s?:number;
v?:number;
outerRadius:any;
innerRadius:any;
className:string;
}) => JSX.Element;
export type RadialHuePicker = (props: {
    h:any;
s?:number;
v?:number;
decimalPrecision?:number;
outerRadius:any;
innerRadius:any;
className:string;
onChange:function;
onDragStart:function;
onDragEnd:function;
}) => JSX.Element;
export type SaturationValueGradient = (props: {
    h:any;
width:any;
height:any;
className:string;
}) => JSX.Element;
export type SaturationValuePicker = (props: {
    h:any;
s:any;
v:any;
decimalPrecision?:number;
width:any;
height:any;
className:string;
onChange:function;
onDragStart:function;
onDragEnd:function;
}) => JSX.Element;
export type ColorPicker = (props: {
    focusKey:UniqueFocusKey | null;
color:any;
alpha:any;
colorWheel?:number;
sliderTextInput?:number;
preview?:number;
mode:any;
hexInput?:number;
onChange:function;
allowFocusExit?:number;
}) => JSX.Element;
export type ColorField = (props: {
    label:any;
value:any;
showAlpha:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type BoundDirectoryPickerButton = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type DirectoryPickerButton = (props: {
    label:any;
value:any;
disabled:boolean;
tooltip:string | JSX.Element | null;
className:string;
theme:any;
onOpenDirectoryBrowser:function;
}) => JSX.Element;
export type BoundEnumField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type BoundFlagsField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type EnumField = (props: {
    label:any;
value:any;
enumMembers:any;
disabled:boolean;
onChange:function;
tooltip:string | JSX.Element | null;
}) => JSX.Element;
export type FlagsField = (props: {
    label:any;
value:any;
enumMembers:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type GradientSliderField = (props: {
    gradient:any;
iconSrc:any;
label:any;
value:any;
min:any;
max:any;
tooltip:string | JSX.Element | null;
onChange:function;
}) => JSX.Element;
export type BoundPopupValueField = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PopupValueField = (props: {
    label:any;
value:any;
expanded:any;
disabled:boolean;
children:string | JSX.Element | JSX.Element[];
tooltip:string | JSX.Element | null;
onExpandedChange:function;
}) => JSX.Element;
export type RangedSliderField = (props: {
    label:any;
iconSrc:any;
value:any;
min:any;
max:any;
fractionDigits?:number;
disabled:boolean;
tooltip:string | JSX.Element | null;
onChange:function;
onChangeStart:function;
onChangeEnd:function;
}) => JSX.Element;
export type SeasonsGridLines = (props: {
    seasons:any;
tickHeight?:number;
}) => JSX.Element;
export type ToggleField = (props: {
    label:any;
value:any;
disabled:boolean;
tooltip:string | JSX.Element | null;
onChange:function;
}) => JSX.Element;
export type BoundFilterMenu = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type FilterMenu = (props: {
    availableFilters:any;
activeFilters:any;
onToggleFilter:function;
onClear:function;
}) => JSX.Element;
export type BoundHierarchyMenu = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type HierarchyMenu = (props: {
    viewport:any;
flex:any;
visibleCount:any;
onSelect:(x: any) => any;
onSetExpanded:function;
singleSelection:any;
onRenderedRangeChange:function;
}) => JSX.Element;
export type ThemeIcon = (props: {
    src:string;
className:string;
}) => JSX.Element;
export type FullWidthDigits = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TabHintIcons = (props: {
    action?:number;
children:string | JSX.Element | JSX.Element[];
hidden:any;
}) => JSX.Element;
export type OrderingSaveListHeader = (props: {
    selectedOrdering:any;
className:string;
options:any;
onSelectOrdering:function;
}) => JSX.Element;
export type CloudTargetSaveListHeader = (props: {
    targets:any;
selectedTarget:any;
className:string;
onSelectTarget:function;
}) => JSX.Element;
export type NewGameMapsSaveListHeader = (props: {
    categories:any;
selectedCategory:any;
className:string;
onSelectCategory:function;
}) => JSX.Element;
export type MessageDialog = (props: {
    title:any;
message:any;
onConfirm:function;
}) => JSX.Element;
export type DetailSection = (props: {
    title:any;
preview:any;
currentPreview:any;
content:any;
footer:any;
className:string;
}) => JSX.Element;
export type Field = (props: {
    label:any;
ellipsis:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type FooterButton = (props: {
    disabled:boolean;
children:string | JSX.Element | JSX.Element[];
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type GameDetail = (props: {
    className:string;
save:any;
onSaveGameSelect:function;
saveName:any;
}) => JSX.Element;
export type SaveSlotItem = (props: {
    focusKey:UniqueFocusKey | null;
locked?:number;
selected?:number;
saveName:any;
saveGame:any;
slotId:any;
inputAction:any;
onSaveNameChange:function;
onSelectSave:function;
onSelect:(x: any) => any;
}) => JSX.Element;
export type SaveSlot = (props: {
    focusKey:UniqueFocusKey | null;
slotId:any;
saveName:any;
saveGame:any;
selected?:number;
className:string;
onSaveNameChange:function;
onSelect:(x: any) => any;
}) => JSX.Element;
export type SaveSlots = (props: {
    sortedSaves:any;
saveName:any;
cloudTarget:any;
slots:any;
selectedSlotId:any;
className:string;
onSaveNameChange:function;
onSelectCloudTarget:function;
onSelectSlot:function;
}) => JSX.Element;
export type SaveItem = (props: {
    save:any;
selected:boolean;
locked:any;
tooltipsInactive:any;
inputAction:any;
onClick:function;
onDoubleClick:function;
onSelect:(x: any) => any;
}) => JSX.Element;
export type BoundItemPicker = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type BoundItemPickerFooter = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type ItemPickerFooter = (props: {
    length:any;
columnCount:any;
onColumnCountChange:function;
}) => JSX.Element;
export type BoundPagedList = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type EditorList = (props: {
    label:any;
disabled:boolean;
children:string | JSX.Element | JSX.Element[];
onAddItem:function;
onClear:function;
}) => JSX.Element;
export type EditorPagedList = (props: {
    label:any;
expanded:any;
tooltip:string | JSX.Element | null;
length:any;
currentPageIndex:any;
pageCount:any;
disabled:boolean;
children:string | JSX.Element | JSX.Element[];
onAddItem:function;
onClear:function;
onExpandedChange:function;
onCurrentPageChange:function;
}) => JSX.Element;
export type EditorListItem = (props: {
    focusKey:UniqueFocusKey | null;
label:any;
disabled:boolean;
first:any;
last:any;
expanded?:number;
children:string | JSX.Element | JSX.Element[];
onMoveUp:function;
onMoveDown:function;
onDuplicate:function;
onDelete:function;
onExpandedChange:function;
}) => JSX.Element;
export type SelectVehiclesSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
primaryVehicle:any;
secondaryVehicle:any;
primaryVehicles:any;
secondaryVehicles:any;
}) => JSX.Element;
export type BoundLocalizationField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type LocalizationField = (props: {
    localization:any;
supportedLocales:any;
onChange:function;
onAdd:function;
onRemove:function;
placeholder:any;
mandatoryId:any;
}) => JSX.Element;
export type BoundPageLayout = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PageLayout = (props: {
    title:any;
className:string;
style:any;
children:string | JSX.Element | JSX.Element[];
onBack:function;
}) => JSX.Element;
export type BoundPageView = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PageView = (props: {
    currentPage:any;
pages:any;
className:string;
style:any;
}) => JSX.Element;
export type BoundRow = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Row = (props: {
    className:string;
style:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BoundScrollable = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type EditorScrollable = (props: {
    className:string;
style:any;
children:string | JSX.Element | JSX.Element[];
vertical?:number;
horizontal?:number;
}) => JSX.Element;
export type BoundPopupSearchField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type PopupSearchField = (props: {
    value:any;
valueIsFavorite:any;
suggestions:any;
onChange:function;
onChangeFavorite:function;
}) => JSX.Element;
export type BoundSearchField = (props: {
    parent:any;
path:any;
props:any;
}) => JSX.Element;
export type SearchField = (props: {
    value:any;
onChange:function;
}) => JSX.Element;
export type BoundEditorSection = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TimeOfDayWeightsChart = (props: {
    props:any;
}) => JSX.Element;
export type EditorWidgetListRenderer = (props: {
    parent:any;
data:any;
}) => JSX.Element;
export type MultiOptionDialog = (props: {
    titleId:any;
messageId:any;
options:any;
}) => JSX.Element;
export type ErrorDialog = (props: {
    messageId:any;
message:any;
}) => JSX.Element;
export type LegalDocumentDialog = (props: {
    text:any;
agreementRequired:any;
confirmLabel:any;
}) => JSX.Element;
export type ParadoxDialogRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type AssetUploadPanelRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type DisplayConfirmationDialogRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InputRebindingDialogRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type LocalizationProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Field = (props: {
    contentClassName:any;
className:string;
children:string | JSX.Element | JSX.Element[];
onSelect:(x: any) => any;
}) => JSX.Element;
export type TimeControls = (props: {
    disablePauseAnimation:any;
editor?:number;
}) => JSX.Element;
export type FreeCameraScreen = (props: {
    onClose:function;
focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type LockedBadge = (props: {
    style:any;
className:string;
}) => JSX.Element;
export type EmptyState = (props: {
    children:string | JSX.Element | JSX.Element[];
className:string;
}) => JSX.Element;
export type Label = (props: {
    props:any;
}) => JSX.Element;
export type Breadcrumbs = (props: {
    children:string | JSX.Element | JSX.Element[];
parent:any;
}) => JSX.Element;
export type OptionWidgetListRenderer = (props: {
    parent:any;
data:any;
}) => JSX.Element;
export type OptionPageHeader = (props: {
    sections:any;
subHeader:any;
selectedId:any;
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type OptionPage = (props: {
    page:any;
selectedSectionId:any;
className:string;
isSearch:any;
subHeader:any;
onSelectSection:function;
}) => JSX.Element;
export type OptionPageFocusedKeyOverride = (props: {
    focusKey:UniqueFocusKey | null;
direction:any;
children:string | JSX.Element | JSX.Element[];
debugName?:number;
}) => JSX.Element;
export type MenuItem = (props: {
    item:any;
selected:boolean;
suffix:any;
beta:any;
onSelect:(x: any) => any;
}) => JSX.Element;
export type PauseMenu = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type UnlockHighlightBadge = (props: {
    className:string;
}) => JSX.Element;
export type ToolButton = (props: {
    focusKey:UniqueFocusKey | null;
src:string;
selected:boolean;
multiSelect:boolean;
disabled:boolean;
tooltip:string | JSX.Element | null;
selectSound:any;
uiTag:string;
className:string;
children:string | JSX.Element | JSX.Element[];
onSelect:(x: any) => any;
shortcut:any;
}) => JSX.Element;
export type ClimateField = (props: {
    hideSeason:any;
theme:any;
}) => JSX.Element;
export type BottomBar = (props: {
    className:string;
onPauseMenuToggle:function;
}) => JSX.Element;
export type AdvisorPanel = (props: {
    expanded:any;
className:string;
focusKey:UniqueFocusKey | null;
localization:any;
theme:any;
onClose:function;
onToggle:function;
}) => JSX.Element;
export type CurveSelector = (props: {
    groupedModifierData:any;
onSelectProperty:function;
activePropertyIndex:any;
}) => JSX.Element;
export type CinematicCameraCurveEditor = (props: {
    label:any;
modifierData:any;
canAddKeyframes:any;
tutorialTag:any;
}) => JSX.Element;
export type CinematicCameraSlider = (props: {
    focusKey:UniqueFocusKey | null;
value:any;
start:any;
end:any;
className:string;
onChange:function;
onDragEnd:function;
}) => JSX.Element;
export type CinematicCameraMainPanel = (props: {
    toggleSaveLoadPopupVisibility:any;
}) => JSX.Element;
export type CinematicCameraPanel = (props: {
    isVisible:any;
onClose:function;
}) => JSX.Element;
export type PhotoModeContainer = (props: {
    title:any;
tooltip:string | JSX.Element | null;
tooltipPos?:number;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PhotoWidgetListRenderer = (props: {
    displayName:any;
parent:any;
data:any;
}) => JSX.Element;
export type PhotoModePanel = (props: {
    onClose:function;
}) => JSX.Element;
export type PhotoModePanelContent = (props: {
    simulationWarning?:number;
}) => JSX.Element;
export type ResizeContainer = (props: {
    width:any;
minWidth:any;
maxWidth:any;
direction:any;
className:string;
children:string | JSX.Element | JSX.Element[];
onResize:function;
}) => JSX.Element;
export type InfomodeItem = (props: {
    focusKey:UniqueFocusKey | null;
infomode:any;
}) => JSX.Element;
export type InfoviewPanelSpace = (props: {
    small?:number;
}) => JSX.Element;
export type InfoviewPanelLabel = (props: {
    icon:any;
text:any;
rightText:any;
uppercase?:number;
centered?:number;
small?:number;
tiny?:number;
}) => JSX.Element;
export type InfoviewPanelIconLabel = (props: {
    icon:any;
label:any;
}) => JSX.Element;
export type AvailabilityBar = (props: {
    value:any;
gradient?:number;
}) => JSX.Element;
export type ValueBar = (props: {
    gradient:any;
value:any;
}) => JSX.Element;
export type InfoviewPanelSection = (props: {
    disableFocus?:number;
tooltip:string | JSX.Element | null;
focusableBorder:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ValueBarSection = (props: {
    title:any;
value:any;
gradient:any;
tooltip:string | JSX.Element | null;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type BarChart = (props: {
    colors:any;
data:any;
className:string;
}) => JSX.Element;
export type InfoBarChart = (props: {
    title:any;
colors:any;
labels:any;
data:any;
ignoreZero?:number;
usePercentageValue?:number;
customLegend:any;
className:string;
}) => JSX.Element;
export type AvailabilityBarSection = (props: {
    title:any;
supplyLabel:any;
demandLabel:any;
supplyValue:any;
demandValue:any;
availability:any;
gradient:any;
tooltip:string | JSX.Element | null;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type Requirements = (props: {
    requirements:any;
theme:any;
className:string;
}) => JSX.Element;
export type TrafficVolumeChart = (props: {
    data:any;
className:string;
}) => JSX.Element;
export type TrafficFlowChart = (props: {
    data:any;
className:string;
}) => JSX.Element;
export type CargoTransportSummary = (props: {
    summaries:any;
}) => JSX.Element;
export type PublicTransportSummary = (props: {
    summaries:any;
}) => JSX.Element;
export type ActiveInfoviewPanel = (props: {
    focusKey:UniqueFocusKey | null;
className:string;
onClose:function;
transition:any;
allowFocusExit:any;
scrollable?:number;
}) => JSX.Element;
export type InfoviewButton = (props: {
    infoview:any;
selected:boolean;
onSelect:(x: any) => any;
bypassLocking:any;
}) => JSX.Element;
export type InfoviewMenu = (props: {
    className:string;
onClose:function;
}) => JSX.Element;
export type EditorInfoviewMenu = (props: {
    className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type Hierarchy = (props: {
    className:string;
}) => JSX.Element;
export type HierarchyPanel = (props: {
    onClose:function;
forceExpanded:any;
}) => JSX.Element;
export type EditorTopPanels = (props: {
    onExitInfoviewFocus:function;
}) => JSX.Element;
export type TooltipRenderer = (props: {
    disabled:boolean;
}) => JSX.Element;
export type BoundTooltipGroup = (props: {
    parent:any;
path:any;
props:any;
children:string | JSX.Element | JSX.Element[];
indexId:any;
}) => JSX.Element;
export type AchievementsWarning = (props: {
    modsEnabled?:number;
pastModsEnabled?:number;
unlockAll?:number;
unlimitedMoney?:number;
unlockMapTiles?:number;
}) => JSX.Element;
export type AchievementsWarningBanner = (props: {
    modsEnabled:any;
pastModsEnabled:any;
unlockAll:any;
unlimitedMoney:any;
unlockMapTiles:any;
className:string;
autoHide?:number;
loadGame?:number;
}) => JSX.Element;
export type ButtonRow = (props: {
    buttons:any;
onSelect:(x: any) => any;
}) => JSX.Element;
export type PropertyField = (props: {
    valueIcon:any;
icon:any;
tinted:any;
label:any;
theme:any;
focusable?:number;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type MapDetail = (props: {
    selectedMap:any;
selectedTheme:any;
activeStep:any;
className:string;
onContinue:function;
onStartGameSelect:function;
}) => JSX.Element;
export type MapItem = (props: {
    map:any;
locked:any;
selected:boolean;
onClick:function;
}) => JSX.Element;
export type OptionField = (props: {
    id:any;
label:any;
theme?:number;
disabled:boolean;
className:string;
children:string | JSX.Element | JSX.Element[];
onClick:function;
sound?:number;
}) => JSX.Element;
export type CityNameField = (props: {
    id:any;
label:any;
value:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type ThemeField = (props: {
    id:any;
label:any;
value:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type ToggleItem = (props: {
    id:any;
value:any;
disabled:boolean;
onChange:function;
}) => JSX.Element;
export type SaveDetail = (props: {
    save:any;
activeStep:any;
className:string;
onOptionsToggle:function;
onLoadGameSelect:function;
}) => JSX.Element;
export type SaveSlot = (props: {
    focusKey:UniqueFocusKey | null;
locked?:number;
saveGame:any;
selected?:number;
slotId:any;
className:string;
onSelectSave:function;
onSelect:(x: any) => any;
}) => JSX.Element;
export type NumberPropertyField = (props: {
    labelId:any;
unit:any;
value:any;
signed:any;
icon:any;
valueIcon:any;
}) => JSX.Element;
export type Number2PropertyField = (props: {
    labelId:any;
unit:any;
value:any;
signed:any;
icon:any;
valueIcon:any;
}) => JSX.Element;
export type StringPropertyField = (props: {
    labelId:any;
valueId:any;
icon:any;
valueIcon:any;
}) => JSX.Element;
export type PrefabPropertyFields = (props: {
    data:any;
}) => JSX.Element;
export type ConstructionCost = (props: {
    value:any;
unit:any;
}) => JSX.Element;
export type ConstructionRangeCost = (props: {
    minValue:any;
maxValue:any;
unit:any;
}) => JSX.Element;
export type NetConstructionCost = (props: {
    value:any;
unit:any;
}) => JSX.Element;
export type DlcBadge = (props: {
    icon:any;
style:any;
className:string;
}) => JSX.Element;
export type AssetGrid = (props: {
    className:string;
}) => JSX.Element;
export type Avatar = (props: {
    entity:any;
className:string;
}) => JSX.Element;
export type AvatarInitials = (props: {
    entity:any;
className:string;
}) => JSX.Element;
export type AvatarButton = (props: {
    entity:any;
focusKey:UniqueFocusKey | null;
showHint?:number;
disabled:boolean;
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type ContainerItem = (props: {
    focusKey:UniqueFocusKey | null;
style:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type EventItem = (props: {
    focusKey:UniqueFocusKey | null;
date:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ChirperPanel = (props: {
    className:string;
onClose:function;
}) => JSX.Element;
export type DetailLayout = (props: {
    icon:any;
title:any;
description:any;
bottom:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type UnlockingLabel = (props: {
    state:any;
}) => JSX.Element;
export type DemandBar = (props: {
    icon:any;
color:any;
demand:any;
locked?:number;
}) => JSX.Element;
export type DemandFactors = (props: {
    factors:any;
locked?:number;
}) => JSX.Element;
export type DemandFactorDetail = (props: {
    factor:any;
}) => JSX.Element;
export type DemandSection = (props: {
    type:any;
demand:any;
unlocking:any;
factors:any;
className:string;
onSelect:(x: any) => any;
onHover:function;
}) => JSX.Element;
export type CityPolicy = (props: {
    policy:any;
selected:boolean;
className:string;
onSelect:(x: any) => any;
onHover:function;
}) => JSX.Element;
export type CityInfoPanel = (props: {
    selectedTab:any;
onSelectTab:function;
onClose:function;
}) => JSX.Element;
export type NumberField = (props: {
    value:any;
importance:any;
className:string;
}) => JSX.Element;
export type BudgetChart = (props: {
    selectedId:any;
className:string;
}) => JSX.Element;
export type BudgetItemDetail = (props: {
    item:any;
values:any;
className:string;
}) => JSX.Element;
export type LoanAdjustment = (props: {
    className:string;
}) => JSX.Element;
export type LoanChart = (props: {
    className:string;
}) => JSX.Element;
export type ResourceDetail = (props: {
    entity:any;
className:string;
}) => JSX.Element;
export type Detail = (props: {
    entity:any;
}) => JSX.Element;
export type InputsColumn = (props: {
    resource:any;
details:any;
}) => JSX.Element;
export type Input = (props: {
    resource:any;
entities:any;
}) => JSX.Element;
export type ExpandableSection = (props: {
    focusKey:UniqueFocusKey | null;
forceFocusable:any;
initialExpanded?:number;
header:any;
theme?:number;
transition?:number;
className:string;
children:string | JSX.Element | JSX.Element[];
onToggleExpanded:function;
}) => JSX.Element;
export type SectionHeader = (props: {
    tooltip:string | JSX.Element | null;
handleMouseEnter:any;
handleMouseLeave:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ResourceList = (props: {
    className:string;
}) => JSX.Element;
export type ServiceItemDetail = (props: {
    service:any;
itemId:any;
className:string;
}) => JSX.Element;
export type BudgetItem = (props: {
    identifier:any;
label:any;
value:any;
}) => JSX.Element;
export type SliderItem = (props: {
    identifier:any;
label:any;
value:any;
min:any;
max:any;
gradient:any;
locked:any;
onValueChange:function;
}) => JSX.Element;
export type BudgetSliderItem = (props: {
    service:any;
value:any;
locked:any;
}) => JSX.Element;
export type ServiceFeeSliderItem = (props: {
    item:any;
locked:any;
}) => JSX.Element;
export type ServiceDetail = (props: {
    service:any;
className:string;
}) => JSX.Element;
export type FeeRevenues = (props: {
    fees:any;
}) => JSX.Element;
export type FeeExpenses = (props: {
    fees:any;
}) => JSX.Element;
export type ServiceList = (props: {
    selectedService:any;
className:string;
onSelectService:function;
}) => JSX.Element;
export type TaxSlider = (props: {
    min:any;
max:any;
rate:any;
income:any;
range:any;
primary:any;
className:string;
disabled:boolean;
onValueChanged:function;
}) => JSX.Element;
export type TaxationItem = (props: {
    areaType:any;
resource:any;
}) => JSX.Element;
export type TaxationGroup = (props: {
    areaType:any;
focused:any;
setFocusedGroup:any;
}) => JSX.Element;
export type EconomyPanel = (props: {
    selectedTab:any;
onSelectTab:function;
onClose:function;
}) => JSX.Element;
export type EventJournalEntry = (props: {
    event:any;
}) => JSX.Element;
export type EventJournal = (props: {
    onClose:function;
}) => JSX.Element;
export type PlaceholderChirpLayout = (props: {
    length:any;
className:string;
}) => JSX.Element;
export type LifePathPanel = (props: {
    selectedCitizen:any;
onClose:function;
}) => JSX.Element;
export type NotificationsPanel = (props: {
    className:string;
}) => JSX.Element;
export type NotificationItem = (props: {
    item:any;
}) => JSX.Element;
export type NotificationsPanel = (props: {
    className:string;
onClose:function;
}) => JSX.Element;
export type Achievement = (props: {
    achievement:any;
selected:boolean;
onHover:function;
onSelect:(x: any) => any;
}) => JSX.Element;
export type MilestoneDetailHeader = (props: {
    details:any;
}) => JSX.Element;
export type UnlockItem = (props: {
    id:any;
selected:boolean;
small:any;
icon:any;
title:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type MilestoneDetail = (props: {
    details:any;
unlocks:any;
selectedUnlock:any;
className:string;
}) => JSX.Element;
export type MilestoneList = (props: {
    selectedMilestone:any;
className:string;
onSelectMilestone:function;
}) => JSX.Element;
export type UnlockDetail = (props: {
    id:any;
milestoneDetails:any;
className:string;
}) => JSX.Element;
export type DevTreeNodeDetail = (props: {
    node:any;
className:string;
}) => JSX.Element;
export type ServiceDetailHeader = (props: {
    details:any;
}) => JSX.Element;
export type ServiceDevTree = (props: {
    service:any;
selectedNode:any;
locked:any;
className:string;
onHoverNode:function;
onSelectNode:function;
}) => JSX.Element;
export type ServiceDetail = (props: {
    service:any;
selectedNode:any;
className:string;
onHoverNode:function;
onSelectNode:function;
}) => JSX.Element;
export type ServiceList = (props: {
    selectedService:any;
className:string;
onSelectService:function;
}) => JSX.Element;
export type ProgressionPanel = (props: {
    selectedTab:any;
onSelectTab:function;
onClose:function;
}) => JSX.Element;
export type RadioContextOverrideProvider = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type RadioContextRedirector = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type RadioContextOverride = (props: {
    enabled:any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type StationDetail = (props: {
    className:string;
}) => JSX.Element;
export type StationsMenu = (props: {
    className:string;
}) => JSX.Element;
export type RadioPanel = (props: {
    className:string;
onClose:function;
}) => JSX.Element;
export type StatisticsGraph = (props: {
    data:any;
selectedStatistics:any;
timeScale:any;
stepCount:any;
onSetTimeScale:function;
className:string;
}) => JSX.Element;
export type StatisticsItem = (props: {
    statItem:any;
selectedStatistics:any;
nesting?:number;
}) => JSX.Element;
export type StatisticsCategory = (props: {
    category:any;
selectedStatistics:any;
}) => JSX.Element;
export type StatisticsMenu = (props: {
    selectedStatistics:any;
}) => JSX.Element;
export type StatisticsPanel = (props: {
    onClose:function;
}) => JSX.Element;
export type TransportLineItem = (props: {
    focusKey:UniqueFocusKey | null;
line:any;
}) => JSX.Element;
export type TransportTypeItem = (props: {
    type:any;
cargo:any;
selected:boolean;
focusKey:UniqueFocusKey | null;
onSelect:(x: any) => any;
}) => JSX.Element;
export type TransportationOverviewPage = (props: {
    cargo:any;
lines:any;
types:any;
selectedType:any;
setSelectedType:any;
}) => JSX.Element;
export type TransportationOverviewPanel = (props: {
    selectedTab:any;
onSelectTab:function;
onClose:function;
}) => JSX.Element;
export type GamePanelRenderer = (props: {
    panel:any;
}) => JSX.Element;
export type SocialPanelLayout = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type PhotoModePanelLayout = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InfoviewMenuToggle = (props: {
    miniPanelVisible:any;
className:string;
}) => JSX.Element;
export type MapTilePurchasePanel = (props: {
    focusKey:UniqueFocusKey | null;
className:string;
onClose:function;
}) => JSX.Element;
export type ButtonRow = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type MilestoneTrophyAnimation = (props: {
    className:string;
}) => JSX.Element;
export type MilestoneUnlockEventPanel = (props: {
    focusKey:UniqueFocusKey | null;
milestone:any;
onClose:function;
}) => JSX.Element;
export type SignatureUnlockEventPanel = (props: {
    focusKey:UniqueFocusKey | null;
entities:any;
onClose:function;
}) => JSX.Element;
export type CapacityInfo = (props: {
    label:any;
value:any;
max:any;
}) => JSX.Element;
export type GenericInfo = (props: {
    label:any;
value:any;
target:any;
}) => JSX.Element;
export type InfoList = (props: {
    label:any;
list:any;
}) => JSX.Element;
export type DeveloperSection = (props: {
    focusKey:UniqueFocusKey | null;
subsections:any;
}) => JSX.Element;
export type DeveloperInfoPanel = (props: {
    onClose:function;
developerInfoSection:any;
}) => JSX.Element;
export type LineVisualizerCanvas = (props: {
    width:any;
height:any;
color:any;
stops:any;
vehicles:any;
segments:any;
stopCapacity:any;
focused:any;
}) => JSX.Element;
export type AttractivenessSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
attractiveness:any;
baseAttractiveness:any;
factors:any;
}) => JSX.Element;
export type CapacityBar = (props: {
    progress:any;
max:any;
plain?:number;
invertColorCodes?:number;
children:string | JSX.Element | JSX.Element[];
className:string;
}) => JSX.Element;
export type BatterySection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
batteryCharge:any;
batteryCapacity:any;
flow:any;
remainingTime:any;
}) => JSX.Element;
export type ComfortSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
comfort:any;
}) => JSX.Element;
export type CompanySection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
companyName:any;
input1:any;
input2:any;
output:any;
sells:any;
stores:any;
customers:any;
}) => JSX.Element;
export type DeathcareSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
bodyCount:any;
bodyCapacity:any;
processingSpeed:any;
processingCapacity:any;
}) => JSX.Element;
export type DescriptionRow = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type InfoButton = (props: {
    label:any;
icon:any;
selected:boolean;
onSelect:(x: any) => any;
}) => JSX.Element;
export type DestroyedBuildingSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
destroyer:any;
cleared:any;
progress:any;
status:any;
}) => JSX.Element;
export type InfoLink = (props: {
    icon:any;
tooltip:string | JSX.Element | null;
uppercase?:number;
onSelect:(x: any) => any;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type DistrictsSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
districtMissing:any;
districts:any;
}) => JSX.Element;
export type EducationSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
studentCount:any;
studentCapacity:any;
graduationTime:any;
failProbability:any;
}) => JSX.Element;
export type EfficiencySection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
efficiency:any;
factors:any;
}) => JSX.Element;
export type ElectricitySection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
capacity:any;
production:any;
}) => JSX.Element;
export type EmployeesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
employeeCount:any;
maxEmployees:any;
educationDataEmployees:any;
educationDataWorkplaces:any;
}) => JSX.Element;
export type FireSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
vehicleEfficiency:any;
}) => JSX.Element;
export type GarbageSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
garbage:any;
garbageCapacity:any;
processingSpeed:any;
processingCapacity:any;
loadKey:any;
}) => JSX.Element;
export type HealthcareSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
patientCount:any;
patientCapacity:any;
}) => JSX.Element;
export type ParkSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
maintenance:any;
}) => JSX.Element;
export type ParkingSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
parkedCars:any;
parkingCapacity:any;
}) => JSX.Element;
export type PoliceSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
prisonerCount:any;
prisonerCapacity:any;
}) => JSX.Element;
export type PollutionSection = (props: {
    groundPollutionKey:any;
airPollutionKey:any;
noisePollutionKey:any;
}) => JSX.Element;
export type PrisonSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
prisonerCount:any;
prisonerCapacity:any;
}) => JSX.Element;
export type ResidentsSection = (props: {
    isHousehold:any;
householdCount:any;
maxHouseholds:any;
residentCount:any;
petCount:any;
wealthKey:any;
residence:any;
residenceEntity:any;
residenceKey:any;
educationData:any;
ageData:any;
}) => JSX.Element;
export type SewageSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
capacity:any;
lastProcessed:any;
purification:any;
}) => JSX.Element;
export type ShelterSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
sheltered:any;
shelterCapacity:any;
}) => JSX.Element;
export type InfoWrapBox = (props: {
    children:string | JSX.Element | JSX.Element[];
className:string;
}) => JSX.Element;
export type StorageSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
stored:any;
capacity:any;
resources:any;
}) => JSX.Element;
export type TransformerSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
capacity:any;
flow:any;
}) => JSX.Element;
export type UpgradePropertiesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
mainBuilding:any;
mainBuildingName:any;
upgrade:any;
type:any;
}) => JSX.Element;
export type NumberPropertyField = (props: {
    labelId:any;
unit:any;
value:any;
signed:any;
}) => JSX.Element;
export type Number2PropertyField = (props: {
    labelId:any;
unit:any;
value:any;
signed:any;
}) => JSX.Element;
export type StringPropertyField = (props: {
    labelId:any;
valueId:any;
}) => JSX.Element;
export type UpgradesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
extensions:any;
subBuildings:any;
}) => JSX.Element;
export type WaterSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
capacity:any;
lastProduction:any;
pollution:any;
}) => JSX.Element;
export type AnimalSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
typeKey:any;
owner:any;
ownerEntity:any;
destination:any;
destinationEntity:any;
}) => JSX.Element;
export type CitizenSection = (props: {
    citizenKey:any;
stateKey:any;
household:any;
householdEntity:any;
residence:any;
residenceEntity:any;
residenceKey:any;
workplace:any;
workplaceEntity:any;
workplaceKey:any;
schoolLevel:any;
tooltipTags:any;
school:any;
schoolEntity:any;
destination:any;
destinationEntity:any;
educationKey:any;
ageKey:any;
wealthKey:any;
occupationKey:any;
jobLevelKey:any;
}) => JSX.Element;
export type DummyHumanSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
origin:any;
originEntity:any;
destination:any;
destinationEntity:any;
}) => JSX.Element;
export type Notification = (props: {
    notification:any;
anchorElRef:any;
}) => JSX.Element;
export type HappinessNotification = (props: {
    notification:any;
anchorElRef:any;
tooltipTags:any;
}) => JSX.Element;
export type AverageHappinessNotification = (props: {
    notification:any;
}) => JSX.Element;
export type ProfitabilityNotification = (props: {
    notification:any;
}) => JSX.Element;
export type ConditionNotification = (props: {
    notification:any;
anchorElRef:any;
tooltipTags:any;
}) => JSX.Element;
export type NotificationBadge = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type StatusSection = (props: {
    conditions:any;
notifications:any;
happiness:any;
focusKey:UniqueFocusKey | null;
tooltipTags:any;
}) => JSX.Element;
export type ColorSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
color:any;
}) => JSX.Element;
export type LinesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
hasLines:any;
lines:any;
hasPassengers:any;
passengers:any;
}) => JSX.Element;
export type LineItem = (props: {
    line:any;
}) => JSX.Element;
export type LocalServicesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
localServiceBuildings:any;
}) => JSX.Element;
export type DestroyedTreeSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
destroyer:any;
}) => JSX.Element;
export type ResourceSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
resourceAmount:any;
resourceKey:any;
}) => JSX.Element;
export type RoadSection = (props: {
    volumeData:any;
flowData:any;
length:any;
bestCondition:any;
worstCondition:any;
condition:any;
upkeep:any;
}) => JSX.Element;
export type LineSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
stops:any;
length:any;
usage:any;
cargo:any;
}) => JSX.Element;
export type ScheduleSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
schedule:any;
}) => JSX.Element;
export type TicketPriceSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
sliderData:any;
}) => JSX.Element;
export type VehicleCountSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
vehicleCount:any;
activeVehicles:any;
vehicleCountMin:any;
vehicleCountMax:any;
}) => JSX.Element;
export type ActionsSection = (props: {
    focusable:any;
focusing:any;
following:any;
followable:any;
moveable:any;
deletable:any;
disabled:boolean;
disableable:any;
emptying:any;
emptiable:any;
hasLotTool:any;
hasTrafficRoutes:any;
focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type DescriptionSection = (props: {
    localeId:any;
effects:any;
}) => JSX.Element;
export type AverageHappinessSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
averageHappiness:any;
happinessFactors:any;
}) => JSX.Element;
export type NotificationsSection = (props: {
    focusKey:UniqueFocusKey | null;
notifications:any;
}) => JSX.Element;
export type ProfitabilitySection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
profitability:any;
profitabilityFactors:any;
}) => JSX.Element;
export type Policy = (props: {
    policy:any;
className:string;
onAdjust:function;
}) => JSX.Element;
export type CompactPolicy = (props: {
    policy:any;
onAdjust:function;
onMouseOver:function;
onMouseLeave:function;
}) => JSX.Element;
export type PoliciesSection = (props: {
    policies:any;
group:any;
tooltipKeys:any;
tooltipTags:any;
}) => JSX.Element;
export type DispatchedVehiclesSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
vehicleList:any;
}) => JSX.Element;
export type CargoSection = (props: {
    group:any;
tooltipTags:any;
tooltipKeys:any;
cargo:any;
capacity:any;
resources:any;
cargoKey:any;
}) => JSX.Element;
export type CargoTransportVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
line:any;
lineEntity:any;
nextStop:any;
stateKey:any;
}) => JSX.Element;
export type DeathcareVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
dead:any;
deadEntity:any;
stateKey:any;
}) => JSX.Element;
export type DeliveryVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type FireVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type GarbageVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type HealthcareVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
patient:any;
patientEntity:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type LoadSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
load:any;
capacity:any;
loadKey:any;
}) => JSX.Element;
export type MaintenanceVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
workShift:any;
owner:any;
fromOutside:any;
nextStop:any;
stateKey:any;
}) => JSX.Element;
export type PassengersSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
passengers:any;
maxPassengers:any;
pets:any;
vehiclePassengerKey:any;
}) => JSX.Element;
export type PoliceVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
criminal:any;
criminalEntity:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type PostVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
nextStop:any;
stateKey:any;
}) => JSX.Element;
export type PrivateVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
keeper:any;
keeperEntity:any;
nextStop:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type PublicTransportVehicleSection = (props: {
    group:any;
tooltipKeys:any;
tooltipTags:any;
owner:any;
fromOutside:any;
line:any;
lineEntity:any;
nextStop:any;
stateKey:any;
vehicleKey:any;
}) => JSX.Element;
export type HouseholdSidebarSection = (props: {
    variant:any;
residence:any;
household:any;
households:any;
residents:any;
pets:any;
focusKey:UniqueFocusKey | null;
className:string;
}) => JSX.Element;
export type PanelSpace = (props: {
    small:any;
}) => JSX.Element;
export type LeftMenu = (props: {
    className:string;
}) => JSX.Element;
export type ChirperPopup = (props: {
    disabled:boolean;
className:string;
onClick:function;
}) => JSX.Element;
export type HorizontalScroller = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type RadioPlayerButton = (props: {
    className:string;
}) => JSX.Element;
export type ButtonBadge = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type AnimatedNumberBadge = (props: {
    value:any;
className:string;
}) => JSX.Element;
export type StatField = (props: {
    icon:any;
value:any;
valueUnit:any;
trend:any;
trendIcon:any;
unlimited?:number;
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type UnlimitedStatField = (props: {
    icon:any;
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type StatFieldTrend = (props: {
    icon:any;
value:any;
valueUnit:any;
trend:any;
thresholdt:any;
unlimited?:number;
className:string;
onSelect:(x: any) => any;
}) => JSX.Element;
export type ProgressionMessageFeed = (props: {
    className:string;
}) => JSX.Element;
export type TutorialActionConsumer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type TutorialWrapper = (props: {
    onTutorialActionNeeded:function;
onSetControlledFocusKey:function;
}) => JSX.Element;
export type UpgradeGrid = (props: {
    className:string;
upgrades:any;
selectedUpgrade:any;
onSelectUpgrade:function;
}) => JSX.Element;
export type UpgradesMenu = (props: {
    focusKey:UniqueFocusKey | null;
showOpenHint:any;
showBackHint:any;
className:string;
}) => JSX.Element;
export type GameMainScreen = (props: {
    focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type TutorialIntroPanel = (props: {
    focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type TutorialListIntroPanel = (props: {
    focusKey:UniqueFocusKey | null;
localization:any;
onCompleteListIntro:function;
}) => JSX.Element;
export type TutorialListOutroPanel = (props: {
    focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type TutorialCard = (props: {
    tutorial:any;
phase:any;
className:string;
expanded:any;
localization:any;
onToggle:function;
onClose:function;
}) => JSX.Element;
export type TutorialCenterCard = (props: {
    tutorial:any;
phase:any;
className:string;
localization:any;
onClose:function;
}) => JSX.Element;
export type TutorialListPanel = (props: {
    list:any;
className:string;
expanded:any;
onToggle:function;
focusKey:UniqueFocusKey | null;
}) => JSX.Element;
export type TutorialListReminder = (props: {
    onClose:function;
className:string;
}) => JSX.Element;
export type Toolbar = (props: {
    className:string;
}) => JSX.Element;
export type EditorMainScreen = (props: {
    focusKey:UniqueFocusKey | null;
className:string;
onPauseMenuToggle:function;
}) => JSX.Element;
export type DebugPrefabToolRenderer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type ParadoxPanel = (props: {
    className:string;
}) => JSX.Element;
export type UserSwitchPromptConsumer = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type CreditsRenderer = (props: {
    lines:any;
images:any;
onComplete:function;
}) => JSX.Element;
export type UserSwitchPrompt = (props: {
    className:string;
}) => JSX.Element;
export type DlcSelector = (props: {
    selected:boolean;
onSelect:(x: any) => any;
}) => JSX.Element;
export type PageSelector = (props: {
    pages:any;
selected:boolean;
onSelect:(x: any) => any;
}) => JSX.Element;
export type WhatsNewPage = (props: {
    page:any;
}) => JSX.Element;
export type WhatsNewTab = (props: {
    tab:any;
selectedPage:any;
}) => JSX.Element;
export type WhatsNewPanel = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type OverlayAction = (props: {
    displayButtonHints?:number;
text:any;
className:string;
}) => JSX.Element;
export type OverlayActions = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type OverlayScreen = (props: {
    actions:any;
className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type OverlayText = (props: {
    className:string;
children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type LoadingProgress = (props: {
    size:any;
lineWidth:any;
progress:any;
progressColors:any;
className:string;
}) => JSX.Element;
export type LogoScreen = (props: {
    children:string | JSX.Element | JSX.Element[];
}) => JSX.Element;
export type SplashScreen = (props: {
    screen:any;
}) => JSX.Element;
export type OverlayUI = (props: {
    forcedScreen:any;
uiReady:any;
}) => JSX.Element;
