/******/ var __webpack_modules__ = ({

/***/ "./src/mods/hello-world.tsx":
/*!**********************************!*\
  !*** ./src/mods/hello-world.tsx ***!
  \**********************************/
/***/ ((__unused_webpack_module, __webpack_exports__, __webpack_require__) => {

__webpack_require__.r(__webpack_exports__);
/* harmony export */ __webpack_require__.d(__webpack_exports__, {
/* harmony export */   HelloWorldComponent: () => (/* binding */ HelloWorldComponent)
/* harmony export */ });
const HelloWorldComponent = () => {
    console.log("Hello Platter!");
    return null;
};


/***/ }),

/***/ "cs2/api":
/*!**************************!*\
  !*** external "cs2/api" ***!
  \**************************/
/***/ ((module) => {

module.exports = window["cs2/api"];

/***/ }),

/***/ "./mod.json":
/*!******************!*\
  !*** ./mod.json ***!
  \******************/
/***/ ((module) => {

module.exports = /*#__PURE__*/JSON.parse('{"id":"Platter","author":"Lucachoo","version":"1.0.0","dependencies":[]}');

/***/ })

/******/ });
/************************************************************************/
/******/ // The module cache
/******/ var __webpack_module_cache__ = {};
/******/ 
/******/ // The require function
/******/ function __webpack_require__(moduleId) {
/******/ 	// Check if module is in cache
/******/ 	var cachedModule = __webpack_module_cache__[moduleId];
/******/ 	if (cachedModule !== undefined) {
/******/ 		return cachedModule.exports;
/******/ 	}
/******/ 	// Create a new module (and put it into the cache)
/******/ 	var module = __webpack_module_cache__[moduleId] = {
/******/ 		// no module.id needed
/******/ 		// no module.loaded needed
/******/ 		exports: {}
/******/ 	};
/******/ 
/******/ 	// Execute the module function
/******/ 	__webpack_modules__[moduleId](module, module.exports, __webpack_require__);
/******/ 
/******/ 	// Return the exports of the module
/******/ 	return module.exports;
/******/ }
/******/ 
/************************************************************************/
/******/ /* webpack/runtime/compat get default export */
/******/ (() => {
/******/ 	// getDefaultExport function for compatibility with non-harmony modules
/******/ 	__webpack_require__.n = (module) => {
/******/ 		var getter = module && module.__esModule ?
/******/ 			() => (module['default']) :
/******/ 			() => (module);
/******/ 		__webpack_require__.d(getter, { a: getter });
/******/ 		return getter;
/******/ 	};
/******/ })();
/******/ 
/******/ /* webpack/runtime/define property getters */
/******/ (() => {
/******/ 	// define getter functions for harmony exports
/******/ 	__webpack_require__.d = (exports, definition) => {
/******/ 		for(var key in definition) {
/******/ 			if(__webpack_require__.o(definition, key) && !__webpack_require__.o(exports, key)) {
/******/ 				Object.defineProperty(exports, key, { enumerable: true, get: definition[key] });
/******/ 			}
/******/ 		}
/******/ 	};
/******/ })();
/******/ 
/******/ /* webpack/runtime/hasOwnProperty shorthand */
/******/ (() => {
/******/ 	__webpack_require__.o = (obj, prop) => (Object.prototype.hasOwnProperty.call(obj, prop))
/******/ })();
/******/ 
/******/ /* webpack/runtime/make namespace object */
/******/ (() => {
/******/ 	// define __esModule on exports
/******/ 	__webpack_require__.r = (exports) => {
/******/ 		if(typeof Symbol !== 'undefined' && Symbol.toStringTag) {
/******/ 			Object.defineProperty(exports, Symbol.toStringTag, { value: 'Module' });
/******/ 		}
/******/ 		Object.defineProperty(exports, '__esModule', { value: true });
/******/ 	};
/******/ })();
/******/ 
/************************************************************************/
var __webpack_exports__ = {};
// This entry needs to be wrapped in an IIFE because it needs to be isolated against other modules in the chunk.
(() => {
/*!***********************!*\
  !*** ./src/index.tsx ***!
  \***********************/
__webpack_require__.r(__webpack_exports__);
/* harmony export */ __webpack_require__.d(__webpack_exports__, {
/* harmony export */   "default": () => (__WEBPACK_DEFAULT_EXPORT__)
/* harmony export */ });
/* harmony import */ var mods_hello_world__WEBPACK_IMPORTED_MODULE_0__ = __webpack_require__(/*! mods/hello-world */ "./src/mods/hello-world.tsx");
/* harmony import */ var cs2_api__WEBPACK_IMPORTED_MODULE_1__ = __webpack_require__(/*! cs2/api */ "cs2/api");
/* harmony import */ var cs2_api__WEBPACK_IMPORTED_MODULE_1___default = /*#__PURE__*/__webpack_require__.n(cs2_api__WEBPACK_IMPORTED_MODULE_1__);
/* harmony import */ var mod_json__WEBPACK_IMPORTED_MODULE_2__ = __webpack_require__(/*! mod.json */ "./mod.json");



const register = (moduleRegistry) => {
    moduleRegistry.append('Menu', mods_hello_world__WEBPACK_IMPORTED_MODULE_0__.HelloWorldComponent);
};
window.Platter = {
    dostuff: (args) => {
        (0,cs2_api__WEBPACK_IMPORTED_MODULE_1__.trigger)(mod_json__WEBPACK_IMPORTED_MODULE_2__.id, "dostuff", args);
    }
};
/* harmony default export */ const __WEBPACK_DEFAULT_EXPORT__ = (register);

})();

var __webpack_exports__default = __webpack_exports__["default"];
const hasCSS = false; export { hasCSS,  __webpack_exports__default as default };
