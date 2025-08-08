# ModernTextViewer Manual Performance Test Script

This document provides a step-by-step manual testing script to validate the performance optimizations implemented in ModernTextViewer. Follow these tests systematically to verify that all performance improvements are working correctly.

## Prerequisites

1. Build the application in Release mode: `dotnet build --configuration Release`
2. Ensure test files are available in the application directory
3. Close any unnecessary applications to get accurate performance measurements
4. Have a stopwatch ready for timing measurements (or use browser developer tools timer)

## Test Files Available

- `test_large_performance.md` (4.6 KB) - Basic testing
- `test_large_4000_words.md` (25.5 KB) - Medium file testing  
- `test_extreme_10000_words.md` (72.3 KB) - Large file stress testing

## Test Script

### Test 1: Basic Application Functionality ✅

**Purpose**: Ensure no regressions in basic functionality

**Steps**:
1. Launch ModernTextViewer
2. Verify application opens without errors
3. Check that UI elements are visible and responsive
4. Test window dragging and resizing
5. Verify toolbar buttons are functional

**Expected Results**:
- Application launches quickly (<2 seconds)
- UI is responsive and modern-looking
- No error dialogs or crashes

**Result**: [ ] PASS [ ] FAIL
**Notes**: ________________________________

---

### Test 2: Small File Loading Performance ✅

**Purpose**: Test basic file loading optimization

**Test File**: `test_large_performance.md` (4.6 KB)

**Steps**:
1. Click "Open" button in toolbar
2. Navigate to and select `test_large_performance.md`
3. Start timer when you click "Open"
4. Stop timer when content appears in editor
5. Verify content loads completely and correctly

**Performance Target**: <200ms
**Expected Results**:
- File loads quickly
- Content appears immediately
- No progress dialog needed for small files
- Text is properly formatted

**Result**: [ ] PASS [ ] FAIL
**Actual Time**: _____ ms
**Notes**: ________________________________

---

### Test 3: Medium File Loading Performance ✅

**Purpose**: Test improved loading for medium-sized files

**Test File**: `test_large_4000_words.md` (25.5 KB)

**Steps**:
1. Click "Open" button
2. Select `test_large_4000_words.md`
3. Start timer at file selection
4. Observe if progress indicator appears
5. Stop timer when file is fully loaded
6. Verify content integrity (scroll through entire document)

**Performance Target**: <500ms
**Expected Results**:
- Loading completes within target time
- Progress indicator may appear briefly
- Full content loads without truncation
- Scrolling is smooth through entire document

**Result**: [ ] PASS [ ] FAIL
**Actual Time**: _____ ms
**Progress Indicator Shown**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 4: Large File Loading Performance ⚡

**Purpose**: Test optimization for large files

**Test File**: `test_extreme_10000_words.md` (72.3 KB)

**Steps**:
1. Click "Open" button
2. Select `test_extreme_10000_words.md`
3. Start timer at file selection
4. Observe progress indicator behavior
5. Monitor UI responsiveness during loading
6. Stop timer when loading completes
7. Test scrolling performance with large content

**Performance Target**: <2000ms (2 seconds)
**Expected Results**:
- Loading completes well within target
- Progress indicator provides user feedback
- UI remains responsive during loading
- Content loads completely
- Scrolling is smooth even with large content

**Result**: [ ] PASS [ ] FAIL
**Actual Time**: _____ ms
**UI Responsive During Loading**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 5: Theme Switching in Raw Mode - Small File 🎨

**Purpose**: Test theme switching optimization with small content

**Test File**: `test_large_performance.md` (loaded)

**Steps**:
1. Ensure file is loaded in raw mode (not preview)
2. Note current theme (dark or light)
3. Start timer
4. Click the theme toggle button
5. Stop timer when theme change is complete
6. Repeat switch back to original theme and time again
7. Test multiple rapid theme switches

**Performance Target**: <100ms per switch
**Expected Results**:
- Theme switches instantly
- No UI freezing or delays
- Colors change immediately across all UI elements
- Text remains properly formatted

**Result**: [ ] PASS [ ] FAIL
**Switch 1 Time**: _____ ms
**Switch 2 Time**: _____ ms
**Rapid Switching Smooth**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 6: Theme Switching in Raw Mode - Large File 🎨⚡

**Purpose**: Test theme switching with large content (critical optimization area)

**Test File**: `test_extreme_10000_words.md` (loaded)

**Steps**:
1. Ensure large file is loaded in raw mode
2. Start timer
3. Click theme toggle button
4. Stop timer when theme change completes
5. Verify entire document reflects new theme
6. Test rapid theme switching (5-10 times quickly)
7. Monitor for any lag or freezing

**Performance Target**: <500ms (was 10+ seconds before optimization)
**Expected Results**:
- Theme switching completes in well under 500ms
- No UI freezing or blocking
- Entire document changes theme consistently
- Rapid switching remains smooth

**Result**: [ ] PASS [ ] FAIL  
**Actual Time**: _____ ms
**Rapid Switching Performance**: [ ] EXCELLENT [ ] GOOD [ ] POOR
**Notes**: ________________________________

---

### Test 7: Preview Mode Toggle Performance ✅

**Purpose**: Test switching between raw and preview modes

**Test File**: `test_large_4000_words.md` (loaded)

**Steps**:
1. Start with file loaded in raw mode
2. Start timer
3. Click "Preview Mode" button/toggle
4. Stop timer when preview rendering is complete
5. Verify markdown is rendered correctly (headers, bold, code blocks)
6. Start timer and switch back to raw mode
7. Stop timer when raw content appears

**Performance Target**: <2000ms for preview initialization
**Expected Results**:
- Preview mode loads within target time
- Markdown renders correctly with formatting
- Raw mode switch is immediate
- WebView2 initializes properly (first time may take longer)

**Result**: [ ] PASS [ ] FAIL
**Raw → Preview Time**: _____ ms
**Preview → Raw Time**: _____ ms
**Markdown Rendering Correct**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 8: Theme Switching in Preview Mode 🎨🌐

**Purpose**: Test optimized theme switching within WebView2 preview

**Test File**: `test_large_4000_words.md` (in preview mode)

**Steps**:
1. Ensure file is in preview mode
2. Start timer
3. Click theme toggle button
4. Stop timer when preview theme changes completely
5. Verify colors change throughout the preview
6. Test rapid theme switching in preview mode
7. Check that content remains properly formatted

**Performance Target**: <500ms (was 15+ seconds with full reload)
**Expected Results**:
- JavaScript-based theme switching is very fast
- Preview colors change immediately
- Content formatting is preserved
- No flashing or reloading visible

**Result**: [ ] PASS [ ] FAIL
**Theme Switch Time**: _____ ms
**JavaScript Optimization Working**: [ ] YES [ ] NO
**Visual Flashing/Reload**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 9: Auto-Save Performance ⚡

**Purpose**: Test that auto-save doesn't impact performance

**Test File**: Any test file

**Steps**:
1. Open any test file
2. Make small edits to trigger "dirty" state
3. Wait for auto-save to trigger (5 minutes) OR modify auto-save timer
4. Observe UI responsiveness during auto-save
5. Verify status bar shows auto-save activity
6. Confirm file is saved correctly

**Performance Target**: No noticeable UI impact
**Expected Results**:
- Auto-save occurs in background
- No UI freezing during save operation
- Status indication shows save progress
- File content is preserved correctly

**Result**: [ ] PASS [ ] FAIL
**UI Impact During Save**: [ ] NONE [ ] MINOR [ ] MAJOR
**Background Operation**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 10: Memory Usage and Long-Term Stability 💾

**Purpose**: Test memory management optimizations

**Steps**:
1. Open Task Manager to monitor memory usage
2. Note baseline memory usage
3. Load each test file sequentially
4. Note memory usage after each file
5. Switch themes multiple times with large file
6. Toggle preview mode several times
7. Close files and force garbage collection (wait 30 seconds)
8. Note final memory usage

**Expected Results**:
- Memory usage grows predictably with file size
- Theme switching doesn't cause memory leaks
- Memory returns to reasonable levels after file closure
- No continuous memory growth over time

**Result**: [ ] PASS [ ] FAIL
**Baseline Memory**: _____ MB
**Peak Memory**: _____ MB
**Final Memory**: _____ MB
**Memory Leaks Detected**: [ ] YES [ ] NO
**Notes**: ________________________________

---

### Test 11: Stress Test - Rapid Operations ⚡🔥

**Purpose**: Test stability under rapid user operations

**Test File**: `test_extreme_10000_words.md`

**Steps**:
1. Load the large test file
2. Rapidly perform the following sequence 10 times:
   - Switch theme
   - Toggle preview mode
   - Switch theme again
   - Toggle back to raw mode
3. Monitor for crashes, hangs, or performance degradation
4. Verify application remains responsive

**Performance Target**: No crashes, consistent performance
**Expected Results**:
- Application handles rapid operations smoothly
- No crashes or error dialogs
- Performance remains consistent throughout test
- UI stays responsive

**Result**: [ ] PASS [ ] FAIL
**Crashes/Errors**: [ ] YES [ ] NO
**Performance Degradation**: [ ] YES [ ] NO
**UI Responsiveness**: [ ] EXCELLENT [ ] GOOD [ ] POOR
**Notes**: ________________________________

---

### Test 12: Edge Cases and Error Handling ⚠️

**Purpose**: Test robustness of optimizations

**Steps**:
1. Test with empty file
2. Test with very small file (1 byte)
3. Test theme switching during file loading
4. Test rapid clicking of theme toggle
5. Simulate WebView2 initialization failure (if possible)
6. Test file operations with network files (if applicable)

**Expected Results**:
- All edge cases handled gracefully
- No crashes or unhandled exceptions
- Appropriate error messages where needed
- Optimizations don't break with unusual inputs

**Result**: [ ] PASS [ ] FAIL
**Edge Cases Handled Well**: [ ] YES [ ] NO
**Error Messages Appropriate**: [ ] YES [ ] NO
**Notes**: ________________________________

---

## Test Results Summary

### Overall Performance Grade

| Test Area | Result | Performance vs Target |
|-----------|--------|-----------------------|
| File Loading (Small) | [ ] PASS [ ] FAIL | _____ vs <200ms |
| File Loading (Medium) | [ ] PASS [ ] FAIL | _____ vs <500ms |
| File Loading (Large) | [ ] PASS [ ] FAIL | _____ vs <2000ms |
| Theme Switch (Raw) | [ ] PASS [ ] FAIL | _____ vs <500ms |
| Theme Switch (Preview) | [ ] PASS [ ] FAIL | _____ vs <500ms |
| Preview Mode Toggle | [ ] PASS [ ] FAIL | _____ vs <2000ms |
| Auto-Save Performance | [ ] PASS [ ] FAIL | UI Impact: _____ |
| Memory Management | [ ] PASS [ ] FAIL | Leak Free: _____ |
| Stress Testing | [ ] PASS [ ] FAIL | Stability: _____ |
| Edge Cases | [ ] PASS [ ] FAIL | Robustness: _____ |

### Critical Success Criteria

- [ ] File loading times meet or exceed targets
- [ ] Theme switching is dramatically faster than baseline (10+ seconds → <500ms)
- [ ] UI remains responsive during all operations
- [ ] No functionality regressions detected
- [ ] Memory usage is stable and efficient
- [ ] Application is stable under stress testing

### Overall Assessment

**Performance Grade**: [ ] A (Excellent) [ ] B (Good) [ ] C (Acceptable) [ ] D (Needs Work) [ ] F (Failed)

**Key Achievements**:
- ________________________________
- ________________________________
- ________________________________

**Areas for Improvement** (if any):
- ________________________________
- ________________________________

**Tester Signature**: _________________ **Date**: _________________

---

## Testing Notes

### Performance Measurement Tips

1. **Timing Accuracy**: Use consistent timing methods (browser dev tools timer, stopwatch app, etc.)
2. **System State**: Close unnecessary programs for accurate measurements
3. **Multiple Runs**: Perform each critical test 2-3 times and average results
4. **Environment**: Note system specifications and any unusual conditions

### Common Issues to Watch For

- UI freezing or becoming unresponsive
- Memory usage continuously increasing
- Theme switching taking longer than expected
- Preview mode failing to initialize
- File content truncation or corruption
- Visual artifacts during theme changes

### Success Indicators

- Operations complete within target times
- UI remains smooth and responsive
- No error messages or crashes
- Visual changes are immediate and complete
- Memory usage is reasonable and stable

This manual test script provides comprehensive validation of all performance optimizations while ensuring no functionality regressions have been introduced.