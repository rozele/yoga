/**
 * Copyright (c) 2014, Facebook, Inc.
 * All rights reserved.
 *
 * This source code is licensed under the BSD-style license found in the
 * LICENSE file in the root directory of this source tree. An additional grant
 * of patent rights can be found in the PATENTS file in the same directory.
 */

using System;
using boolean = System.Boolean;

namespace Facebook.CSSLayout
{

    /**
     * Calculates layouts based on CSS style. See {@link #layoutNode(CSSNode, float)}.
     */

    static class LayoutEngine
    {
        const int POSITION_LEFT = CSSLayout.POSITION_LEFT;
        const int POSITION_TOP = CSSLayout.POSITION_TOP;
        const int POSITION_RIGHT = CSSLayout.POSITION_RIGHT;
        const int POSITION_BOTTOM = CSSLayout.POSITION_BOTTOM;
        const int DIMENSION_WIDTH = CSSLayout.DIMENSION_WIDTH;
        const int DIMENSION_HEIGHT = CSSLayout.DIMENSION_HEIGHT;

        const int CSS_FLEX_DIRECTION_COLUMN =
            (int)CSSFlexDirection.Column;
        const int CSS_FLEX_DIRECTION_COLUMN_REVERSE =
            (int)CSSFlexDirection.ColumnReverse;
        const int CSS_FLEX_DIRECTION_ROW =
            (int)CSSFlexDirection.Row;
        const int CSS_FLEX_DIRECTION_ROW_REVERSE =
            (int)CSSFlexDirection.RowReverse;

        const int CSS_POSITION_RELATIVE = (int)CSSPositionType.Relative;
        const int CSS_POSITION_ABSOLUTE = (int)CSSPositionType.Absolute;

        private static readonly int[] leading = {
            POSITION_TOP,
            POSITION_BOTTOM,
            POSITION_LEFT,
            POSITION_RIGHT,
        };

        private static readonly int[] trailing = {
            POSITION_BOTTOM,
            POSITION_TOP,
            POSITION_RIGHT,
            POSITION_LEFT,
        };

        private static readonly int[] pos = {
            POSITION_TOP,
            POSITION_BOTTOM,
            POSITION_LEFT,
            POSITION_RIGHT,
        };

        private static readonly int[] dim = {
            DIMENSION_HEIGHT,
            DIMENSION_HEIGHT,
            DIMENSION_WIDTH,
            DIMENSION_WIDTH,
        };

        private static readonly int[] leadingSpacing = {
            Spacing.TOP,
            Spacing.BOTTOM,
            Spacing.START,
            Spacing.START
        };

        private static readonly int[] trailingSpacing = {
            Spacing.BOTTOM,
            Spacing.TOP,
            Spacing.END,
            Spacing.END
        };

        private static float boundAxis(CSSNode node, int axis, float value)
        {
            float min = CSSConstants.Undefined;
            float max = CSSConstants.Undefined;

            if (axis == CSS_FLEX_DIRECTION_COLUMN || axis == CSS_FLEX_DIRECTION_COLUMN_REVERSE)
            {
                min = node.style.minHeight;
                max = node.style.maxHeight;
            }
            else if (axis == CSS_FLEX_DIRECTION_ROW || axis == CSS_FLEX_DIRECTION_ROW_REVERSE)
            {
                min = node.style.minWidth;
                max = node.style.maxWidth;
            }

            float boundValue = value;

            if (!float.IsNaN(max) && max >= 0.0 && boundValue > max)
            {
                boundValue = max;
            }
            if (!float.IsNaN(min) && min >= 0.0 && boundValue < min)
            {
                boundValue = min;
            }

            return boundValue;
        }

        private static void setDimensionFromStyle(CSSNode node, int axis)
        {
            // The parent already computed us a width or height. We just skip it
            if (!float.IsNaN(node.layout.dimensions[dim[axis]]))
            {
                return;
            }
            // We only run if there's a width or height defined
            if (float.IsNaN(node.style.dimensions[dim[axis]]) ||
                            node.style.dimensions[dim[axis]] <= 0.0)
            {
                return;
            }

            // The dimensions can never be smaller than the padding and border
            float maxLayoutDimension = Math.Max(
                boundAxis(node, axis, node.style.dimensions[dim[axis]]),
                node.style.padding.getWithFallback(leadingSpacing[axis], leading[axis]) +
                node.style.padding.getWithFallback(trailingSpacing[axis], trailing[axis]) +
                node.style.border.getWithFallback(leadingSpacing[axis], leading[axis]) +
                node.style.border.getWithFallback(trailingSpacing[axis], trailing[axis]));
            node.layout.dimensions[dim[axis]] = maxLayoutDimension;
        }

        private static float getRelativePosition(CSSNode node, int axis)
        {
            float lead = node.style.position[leading[axis]];
            if (!float.IsNaN(lead))
            {
                return lead;
            }

            float trailingPos = node.style.position[trailing[axis]];
            return float.IsNaN(trailingPos) ? 0 : -trailingPos;
        }

        static int resolveAxis(int axis, CSSDirection direction)
        {
            if (direction == CSSDirection.RTL)
            {
                if (axis == CSS_FLEX_DIRECTION_ROW)
                {
                    return CSS_FLEX_DIRECTION_ROW_REVERSE;
                }
                else if (axis == CSS_FLEX_DIRECTION_ROW_REVERSE)
                {
                    return CSS_FLEX_DIRECTION_ROW;
                }
            }

            return axis;
        }

        static CSSDirection resolveDirection(CSSNode node, CSSDirection? parentDirection)
        {
            CSSDirection direction = node.style.direction;
            if (direction == CSSDirection.Inherit)
            {
                direction = (parentDirection == null ? CSSDirection.LTR : parentDirection.Value);
            }

            return direction;
        }

        static int getFlexDirection(CSSNode node)
        {
            return (int)node.style.flexDirection;
        }

        private static int getCrossFlexDirection(int axis, CSSDirection direction)
        {
            if (axis == CSS_FLEX_DIRECTION_COLUMN || axis == CSS_FLEX_DIRECTION_COLUMN_REVERSE)
            {
                return resolveAxis(CSS_FLEX_DIRECTION_ROW, direction);
            }
            else
            {
                return CSS_FLEX_DIRECTION_COLUMN;
            }
        }

        static CSSAlign getAlignItem(CSSNode node, CSSNode child)
        {
            if (child.style.alignSelf != CSSAlign.Auto)
            {
                return child.style.alignSelf;
            }
            return node.style.alignItems;
        }

        static boolean isMeasureDefined(CSSNode node)
        {
            return node.IsMeasureDefined;
        }

        static boolean needsRelayout(CSSNode node, float parentMaxWidth)
        {
            return node.isDirty() ||
                !FloatUtil.floatsEqual(
                    node.lastLayout.requestedHeight,
                    node.layout.dimensions[DIMENSION_HEIGHT]) ||
                !FloatUtil.floatsEqual(
                    node.lastLayout.requestedWidth,
                    node.layout.dimensions[DIMENSION_WIDTH]) ||
                !FloatUtil.floatsEqual(node.lastLayout.parentMaxWidth, parentMaxWidth);
        }

        internal static void layoutNode(CSSLayoutContext layoutContext, CSSNode node, float parentMaxWidth, CSSDirection? parentDirection)
        {
            if (needsRelayout(node, parentMaxWidth))
            {
                node.lastLayout.requestedWidth = node.layout.dimensions[DIMENSION_WIDTH];
                node.lastLayout.requestedHeight = node.layout.dimensions[DIMENSION_HEIGHT];
                node.lastLayout.parentMaxWidth = parentMaxWidth;

                layoutNodeImpl(layoutContext, node, parentMaxWidth, parentDirection);
                node.lastLayout.copy(node.layout);
            }
            else
            {
                node.layout.copy(node.lastLayout);
            }

            node.markHasNewLayout();
        }

        static void layoutNodeImpl(CSSLayoutContext layoutContext, CSSNode node, float parentMaxWidth, CSSDirection? parentDirection)
        {
            var childCount_ = node.getChildCount();
            for (int i_ = 0; i_ < childCount_; i_++)
            {
                node.getChildAt(i_).layout.resetResult();
            }


            /** START_GENERATED **/
    
      CSSDirection direction = resolveDirection(node, parentDirection);
      int mainAxis = resolveAxis(getFlexDirection(node), direction);
      int crossAxis = getCrossFlexDirection(mainAxis, direction);
      int resolvedRowAxis = resolveAxis(CSS_FLEX_DIRECTION_ROW, direction);
    
      // Handle width and height style attributes
      setDimensionFromStyle(node, mainAxis);
      setDimensionFromStyle(node, crossAxis);
    
      // Set the resolved resolution in the node's layout
      node.layout.direction = direction;
    
      // The position is set by the parent, but we need to complete it with a
      // delta composed of the margin and left/top/right/bottom
      node.layout.position[leading[mainAxis]] += node.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) +
        getRelativePosition(node, mainAxis);
      node.layout.position[trailing[mainAxis]] += node.style.margin.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) +
        getRelativePosition(node, mainAxis);
      node.layout.position[leading[crossAxis]] += node.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) +
        getRelativePosition(node, crossAxis);
      node.layout.position[trailing[crossAxis]] += node.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]) +
        getRelativePosition(node, crossAxis);
    
      // Inline immutable values from the target node to avoid excessive method
      // invocations during the layout calculation.
      int childCount = node.getChildCount();
      float paddingAndBorderAxisResolvedRow = ((node.style.padding.getWithFallback(leadingSpacing[resolvedRowAxis], leading[resolvedRowAxis]) + node.style.border.getWithFallback(leadingSpacing[resolvedRowAxis], leading[resolvedRowAxis])) + (node.style.padding.getWithFallback(trailingSpacing[resolvedRowAxis], trailing[resolvedRowAxis]) + node.style.border.getWithFallback(trailingSpacing[resolvedRowAxis], trailing[resolvedRowAxis])));
    
      if (isMeasureDefined(node)) {
        boolean isResolvedRowDimDefined = !float.IsNaN(node.layout.dimensions[dim[resolvedRowAxis]]);
    
        float width = CSSConstants.Undefined;
        if ((!float.IsNaN(node.style.dimensions[dim[resolvedRowAxis]]) && node.style.dimensions[dim[resolvedRowAxis]] >= 0.0)) {
          width = node.style.dimensions[DIMENSION_WIDTH];
        } else if (isResolvedRowDimDefined) {
          width = node.layout.dimensions[dim[resolvedRowAxis]];
        } else {
          width = parentMaxWidth -
            (node.style.margin.getWithFallback(leadingSpacing[resolvedRowAxis], leading[resolvedRowAxis]) + node.style.margin.getWithFallback(trailingSpacing[resolvedRowAxis], trailing[resolvedRowAxis]));
        }
        width -= paddingAndBorderAxisResolvedRow;
    
        // We only need to give a dimension for the text if we haven't got any
        // for it computed yet. It can either be from the style attribute or because
        // the element is flexible.
        boolean isRowUndefined = !(!float.IsNaN(node.style.dimensions[dim[resolvedRowAxis]]) && node.style.dimensions[dim[resolvedRowAxis]] >= 0.0) && !isResolvedRowDimDefined;
        boolean isColumnUndefined = !(!float.IsNaN(node.style.dimensions[dim[CSS_FLEX_DIRECTION_COLUMN]]) && node.style.dimensions[dim[CSS_FLEX_DIRECTION_COLUMN]] >= 0.0) &&
          float.IsNaN(node.layout.dimensions[dim[CSS_FLEX_DIRECTION_COLUMN]]);
    
        // Let's not measure the text if we already know both dimensions
        if (isRowUndefined || isColumnUndefined) {
          MeasureOutput measureDim = node.measure(
            
            layoutContext.measureOutput,
            width
          );
          if (isRowUndefined) {
            node.layout.dimensions[DIMENSION_WIDTH] = measureDim.width +
              paddingAndBorderAxisResolvedRow;
          }
          if (isColumnUndefined) {
            node.layout.dimensions[DIMENSION_HEIGHT] = measureDim.height +
              ((node.style.padding.getWithFallback(leadingSpacing[CSS_FLEX_DIRECTION_COLUMN], leading[CSS_FLEX_DIRECTION_COLUMN]) + node.style.border.getWithFallback(leadingSpacing[CSS_FLEX_DIRECTION_COLUMN], leading[CSS_FLEX_DIRECTION_COLUMN])) + (node.style.padding.getWithFallback(trailingSpacing[CSS_FLEX_DIRECTION_COLUMN], trailing[CSS_FLEX_DIRECTION_COLUMN]) + node.style.border.getWithFallback(trailingSpacing[CSS_FLEX_DIRECTION_COLUMN], trailing[CSS_FLEX_DIRECTION_COLUMN])));
          }
        }
        if (childCount == 0) {
          return;
        }
      }
    
      boolean isNodeFlexWrap = (node.style.flexWrap == CSSWrap.Wrap);
    
      CSSJustify justifyContent = node.style.justifyContent;
    
      float leadingPaddingAndBorderMain = (node.style.padding.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + node.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]));
      float leadingPaddingAndBorderCross = (node.style.padding.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + node.style.border.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]));
      float paddingAndBorderAxisMain = ((node.style.padding.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + node.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis])) + (node.style.padding.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) + node.style.border.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis])));
      float paddingAndBorderAxisCross = ((node.style.padding.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + node.style.border.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis])) + (node.style.padding.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]) + node.style.border.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis])));
    
      boolean isMainDimDefined = !float.IsNaN(node.layout.dimensions[dim[mainAxis]]);
      boolean isCrossDimDefined = !float.IsNaN(node.layout.dimensions[dim[crossAxis]]);
      boolean isMainRowDirection = (mainAxis == CSS_FLEX_DIRECTION_ROW || mainAxis == CSS_FLEX_DIRECTION_ROW_REVERSE);
    
      int i;
      int ii;
      CSSNode child;
      int axis;
    
      CSSNode firstAbsoluteChild = null;
      CSSNode currentAbsoluteChild = null;
    
      float definedMainDim = CSSConstants.Undefined;
      if (isMainDimDefined) {
        definedMainDim = node.layout.dimensions[dim[mainAxis]] - paddingAndBorderAxisMain;
      }
    
      // We want to execute the next two loops one per line with flex-wrap
      int startLine = 0;
      int endLine = 0;
      // int nextOffset = 0;
      int alreadyComputedNextLayout = 0;
      // We aggregate the total dimensions of the container in those two variables
      float linesCrossDim = 0;
      float linesMainDim = 0;
      int linesCount = 0;
      while (endLine < childCount) {
        // <Loop A> Layout non flexible children and count children by type
    
        // mainContentDim is accumulation of the dimensions and margin of all the
        // non flexible children. This will be used in order to either set the
        // dimensions of the node if none already exist, or to compute the
        // remaining space left for the flexible children.
        float mainContentDim = 0;
    
        // There are three kind of children, non flexible, flexible and absolute.
        // We need to know how many there are in order to distribute the space.
        int flexibleChildrenCount = 0;
        float totalFlexible = 0;
        int nonFlexibleChildrenCount = 0;
    
        // Use the line loop to position children in the main axis for as long
        // as they are using a simple stacking behaviour. Children that are
        // immediately stacked in the initial loop will not be touched again
        // in <Loop C>.
        boolean isSimpleStackMain =
            (isMainDimDefined && justifyContent == CSSJustify.FlexStart) ||
            (!isMainDimDefined && justifyContent != CSSJustify.Center);
        int firstComplexMain = (isSimpleStackMain ? childCount : startLine);
    
        // Use the initial line loop to position children in the cross axis for
        // as long as they are relatively positioned with alignment STRETCH or
        // FLEX_START. Children that are immediately stacked in the initial loop
        // will not be touched again in <Loop D>.
        boolean isSimpleStackCross = true;
        int firstComplexCross = childCount;
    
        CSSNode firstFlexChild = null;
        CSSNode currentFlexChild = null;
    
        float mainDim = leadingPaddingAndBorderMain;
        float crossDim = 0;
    
        float maxWidth;
        for (i = startLine; i < childCount; ++i) {
          child = node.getChildAt(i);
          child.lineIndex = linesCount;
    
          child.nextAbsoluteChild = null;
          child.nextFlexChild = null;
    
          CSSAlign alignItem = getAlignItem(node, child);
    
          // Pre-fill cross axis dimensions when the child is using stretch before
          // we call the recursive layout pass
          if (alignItem == CSSAlign.Stretch &&
              child.style.positionType == CSSPositionType.Relative &&
              isCrossDimDefined &&
              !(!float.IsNaN(child.style.dimensions[dim[crossAxis]]) && child.style.dimensions[dim[crossAxis]] >= 0.0)) {
            child.layout.dimensions[dim[crossAxis]] = Math.Max(
              boundAxis(child, crossAxis, node.layout.dimensions[dim[crossAxis]] -
                paddingAndBorderAxisCross - (child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]))),
              // You never want to go smaller than padding
              ((child.style.padding.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.border.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis])) + (child.style.padding.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]) + child.style.border.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis])))
            );
          } else if (child.style.positionType == CSSPositionType.Absolute) {
            // Store a private linked list of absolutely positioned children
            // so that we can efficiently traverse them later.
            if (firstAbsoluteChild == null) {
              firstAbsoluteChild = child;
            }
            if (currentAbsoluteChild != null) {
              currentAbsoluteChild.nextAbsoluteChild = child;
            }
            currentAbsoluteChild = child;
    
            // Pre-fill dimensions when using absolute position and both offsets for the axis are defined (either both
            // left and right or top and bottom).
            for (ii = 0; ii < 2; ii++) {
              axis = (ii != 0) ? CSS_FLEX_DIRECTION_ROW : CSS_FLEX_DIRECTION_COLUMN;
              if (!float.IsNaN(node.layout.dimensions[dim[axis]]) &&
                  !(!float.IsNaN(child.style.dimensions[dim[axis]]) && child.style.dimensions[dim[axis]] >= 0.0) &&
                  !float.IsNaN(child.style.position[leading[axis]]) &&
                  !float.IsNaN(child.style.position[trailing[axis]])) {
                child.layout.dimensions[dim[axis]] = Math.Max(
                  boundAxis(child, axis, node.layout.dimensions[dim[axis]] -
                    ((node.style.padding.getWithFallback(leadingSpacing[axis], leading[axis]) + node.style.border.getWithFallback(leadingSpacing[axis], leading[axis])) + (node.style.padding.getWithFallback(trailingSpacing[axis], trailing[axis]) + node.style.border.getWithFallback(trailingSpacing[axis], trailing[axis]))) -
                    (child.style.margin.getWithFallback(leadingSpacing[axis], leading[axis]) + child.style.margin.getWithFallback(trailingSpacing[axis], trailing[axis])) -
                    (float.IsNaN(child.style.position[leading[axis]]) ?  0 : child.style.position[leading[axis]]) -
                    (float.IsNaN(child.style.position[trailing[axis]]) ?  0 : child.style.position[trailing[axis]])),
                  // You never want to go smaller than padding
                  ((child.style.padding.getWithFallback(leadingSpacing[axis], leading[axis]) + child.style.border.getWithFallback(leadingSpacing[axis], leading[axis])) + (child.style.padding.getWithFallback(trailingSpacing[axis], trailing[axis]) + child.style.border.getWithFallback(trailingSpacing[axis], trailing[axis])))
                );
              }
            }
          }
    
          float nextContentDim = 0;
    
          // It only makes sense to consider a child flexible if we have a computed
          // dimension for the node.
          if (isMainDimDefined && (child.style.positionType == CSSPositionType.Relative && child.style.flex > 0)) {
            flexibleChildrenCount++;
            totalFlexible += child.style.flex;
    
            // Store a private linked list of flexible children so that we can
            // efficiently traverse them later.
            if (firstFlexChild == null) {
              firstFlexChild = child;
            }
            if (currentFlexChild != null) {
              currentFlexChild.nextFlexChild = child;
            }
            currentFlexChild = child;
    
            // Even if we don't know its exact size yet, we already know the padding,
            // border and margin. We'll use this partial information, which represents
            // the smallest possible size for the child, to compute the remaining
            // available space.
            nextContentDim = ((child.style.padding.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + child.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis])) + (child.style.padding.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) + child.style.border.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]))) +
              (child.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + child.style.margin.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]));
    
          } else {
            maxWidth = CSSConstants.Undefined;
            if (!isMainRowDirection) {
              if ((!float.IsNaN(node.style.dimensions[dim[resolvedRowAxis]]) && node.style.dimensions[dim[resolvedRowAxis]] >= 0.0)) {
                maxWidth = node.layout.dimensions[dim[resolvedRowAxis]] -
                  paddingAndBorderAxisResolvedRow;
              } else {
                maxWidth = parentMaxWidth -
                  (node.style.margin.getWithFallback(leadingSpacing[resolvedRowAxis], leading[resolvedRowAxis]) + node.style.margin.getWithFallback(trailingSpacing[resolvedRowAxis], trailing[resolvedRowAxis])) -
                  paddingAndBorderAxisResolvedRow;
              }
            }
    
            // This is the main recursive call. We layout non flexible children.
            if (alreadyComputedNextLayout == 0) {
              layoutNode(layoutContext, child, maxWidth, direction);
            }
    
            // Absolute positioned elements do not take part of the layout, so we
            // don't use them to compute mainContentDim
            if (child.style.positionType == CSSPositionType.Relative) {
              nonFlexibleChildrenCount++;
              // At this point we know the final size and margin of the element.
              nextContentDim = (child.layout.dimensions[dim[mainAxis]] + child.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + child.style.margin.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]));
            }
          }
    
          // The element we are about to add would make us go to the next line
          if (isNodeFlexWrap &&
              isMainDimDefined &&
              mainContentDim + nextContentDim > definedMainDim &&
              // If there's only one element, then it's bigger than the content
              // and needs its own line
              i != startLine) {
            nonFlexibleChildrenCount--;
            alreadyComputedNextLayout = 1;
            break;
          }
    
          // Disable simple stacking in the main axis for the current line as
          // we found a non-trivial child. The remaining children will be laid out
          // in <Loop C>.
          if (isSimpleStackMain &&
              (child.style.positionType != CSSPositionType.Relative || (child.style.positionType == CSSPositionType.Relative && child.style.flex > 0))) {
            isSimpleStackMain = false;
            firstComplexMain = i;
          }
    
          // Disable simple stacking in the cross axis for the current line as
          // we found a non-trivial child. The remaining children will be laid out
          // in <Loop D>.
          if (isSimpleStackCross &&
              (child.style.positionType != CSSPositionType.Relative ||
                  (alignItem != CSSAlign.Stretch && alignItem != CSSAlign.FlexStart) ||
                  float.IsNaN(child.layout.dimensions[dim[crossAxis]]))) {
            isSimpleStackCross = false;
            firstComplexCross = i;
          }
    
          if (isSimpleStackMain) {
            child.layout.position[pos[mainAxis]] += mainDim;
            if (isMainDimDefined) {
              child.layout.position[trailing[mainAxis]] = node.layout.dimensions[dim[mainAxis]] - child.layout.dimensions[dim[mainAxis]] - child.layout.position[pos[mainAxis]];
            }
    
            mainDim += (child.layout.dimensions[dim[mainAxis]] + child.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + child.style.margin.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]));
            crossDim = Math.Max(crossDim, boundAxis(child, crossAxis, (child.layout.dimensions[dim[crossAxis]] + child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]))));
          }
    
          if (isSimpleStackCross) {
            child.layout.position[pos[crossAxis]] += linesCrossDim + leadingPaddingAndBorderCross;
            if (isCrossDimDefined) {
              child.layout.position[trailing[crossAxis]] = node.layout.dimensions[dim[crossAxis]] - child.layout.dimensions[dim[crossAxis]] - child.layout.position[pos[crossAxis]];
            }
          }
    
          alreadyComputedNextLayout = 0;
          mainContentDim += nextContentDim;
          endLine = i + 1;
        }
    
        // <Loop B> Layout flexible children and allocate empty space
    
        // In order to position the elements in the main axis, we have two
        // controls. The space between the beginning and the first element
        // and the space between each two elements.
        float leadingMainDim = 0;
        float betweenMainDim = 0;
    
        // The remaining available space that needs to be allocated
        float remainingMainDim = 0;
        if (isMainDimDefined) {
          remainingMainDim = definedMainDim - mainContentDim;
        } else {
          remainingMainDim = Math.Max(mainContentDim, 0) - mainContentDim;
        }
    
        // If there are flexible children in the mix, they are going to fill the
        // remaining space
        if (flexibleChildrenCount != 0) {
          float flexibleMainDim = remainingMainDim / totalFlexible;
          float baseMainDim;
          float boundMainDim;
    
          // If the flex share of remaining space doesn't meet min/max bounds,
          // remove this child from flex calculations.
          currentFlexChild = firstFlexChild;
          while (currentFlexChild != null) {
            baseMainDim = flexibleMainDim * currentFlexChild.style.flex +
                ((currentFlexChild.style.padding.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + currentFlexChild.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis])) + (currentFlexChild.style.padding.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) + currentFlexChild.style.border.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis])));
            boundMainDim = boundAxis(currentFlexChild, mainAxis, baseMainDim);
    
            if (baseMainDim != boundMainDim) {
              remainingMainDim -= boundMainDim;
              totalFlexible -= currentFlexChild.style.flex;
            }
    
            currentFlexChild = currentFlexChild.nextFlexChild;
          }
          flexibleMainDim = remainingMainDim / totalFlexible;
    
          // The non flexible children can overflow the container, in this case
          // we should just assume that there is no space available.
          if (flexibleMainDim < 0) {
            flexibleMainDim = 0;
          }
    
          currentFlexChild = firstFlexChild;
          while (currentFlexChild != null) {
            // At this point we know the final size of the element in the main
            // dimension
            currentFlexChild.layout.dimensions[dim[mainAxis]] = boundAxis(currentFlexChild, mainAxis,
              flexibleMainDim * currentFlexChild.style.flex +
                  ((currentFlexChild.style.padding.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + currentFlexChild.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis])) + (currentFlexChild.style.padding.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) + currentFlexChild.style.border.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis])))
            );
    
            maxWidth = CSSConstants.Undefined;
            if ((!float.IsNaN(node.style.dimensions[dim[resolvedRowAxis]]) && node.style.dimensions[dim[resolvedRowAxis]] >= 0.0)) {
              maxWidth = node.layout.dimensions[dim[resolvedRowAxis]] -
                paddingAndBorderAxisResolvedRow;
            } else if (!isMainRowDirection) {
              maxWidth = parentMaxWidth -
                (node.style.margin.getWithFallback(leadingSpacing[resolvedRowAxis], leading[resolvedRowAxis]) + node.style.margin.getWithFallback(trailingSpacing[resolvedRowAxis], trailing[resolvedRowAxis])) -
                paddingAndBorderAxisResolvedRow;
            }
    
            // And we recursively call the layout algorithm for this child
            layoutNode(layoutContext, currentFlexChild, maxWidth, direction);
    
            child = currentFlexChild;
            currentFlexChild = currentFlexChild.nextFlexChild;
            child.nextFlexChild = null;
          }
    
        // We use justifyContent to figure out how to allocate the remaining
        // space available
        } else if (justifyContent != CSSJustify.FlexStart) {
          if (justifyContent == CSSJustify.Center) {
            leadingMainDim = remainingMainDim / 2;
          } else if (justifyContent == CSSJustify.FlexEnd) {
            leadingMainDim = remainingMainDim;
          } else if (justifyContent == CSSJustify.SpaceBetween) {
            remainingMainDim = Math.Max(remainingMainDim, 0);
            if (flexibleChildrenCount + nonFlexibleChildrenCount - 1 != 0) {
              betweenMainDim = remainingMainDim /
                (flexibleChildrenCount + nonFlexibleChildrenCount - 1);
            } else {
              betweenMainDim = 0;
            }
          } else if (justifyContent == CSSJustify.SpaceAround) {
            // Space on the edges is half of the space between elements
            betweenMainDim = remainingMainDim /
              (flexibleChildrenCount + nonFlexibleChildrenCount);
            leadingMainDim = betweenMainDim / 2;
          }
        }
    
        // <Loop C> Position elements in the main axis and compute dimensions
    
        // At this point, all the children have their dimensions set. We need to
        // find their position. In order to do that, we accumulate data in
        // variables that are also useful to compute the total dimensions of the
        // container!
        mainDim += leadingMainDim;
    
        for (i = firstComplexMain; i < endLine; ++i) {
          child = node.getChildAt(i);
    
          if (child.style.positionType == CSSPositionType.Absolute &&
              !float.IsNaN(child.style.position[leading[mainAxis]])) {
            // In case the child is position absolute and has left/top being
            // defined, we override the position to whatever the user said
            // (and margin/border).
            child.layout.position[pos[mainAxis]] = (float.IsNaN(child.style.position[leading[mainAxis]]) ?  0 : child.style.position[leading[mainAxis]]) +
              node.style.border.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) +
              child.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]);
          } else {
            // If the child is position absolute (without top/left) or relative,
            // we put it at the current accumulated offset.
            child.layout.position[pos[mainAxis]] += mainDim;
    
            // Define the trailing position accordingly.
            if (isMainDimDefined) {
              child.layout.position[trailing[mainAxis]] = node.layout.dimensions[dim[mainAxis]] - child.layout.dimensions[dim[mainAxis]] - child.layout.position[pos[mainAxis]];
            }
    
            // Now that we placed the element, we need to update the variables
            // We only need to do that for relative elements. Absolute elements
            // do not take part in that phase.
            if (child.style.positionType == CSSPositionType.Relative) {
              // The main dimension is the sum of all the elements dimension plus
              // the spacing.
              mainDim += betweenMainDim + (child.layout.dimensions[dim[mainAxis]] + child.style.margin.getWithFallback(leadingSpacing[mainAxis], leading[mainAxis]) + child.style.margin.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]));
              // The cross dimension is the max of the elements dimension since there
              // can only be one element in that cross dimension.
              crossDim = Math.Max(crossDim, boundAxis(child, crossAxis, (child.layout.dimensions[dim[crossAxis]] + child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]))));
            }
          }
        }
    
        float containerCrossAxis = node.layout.dimensions[dim[crossAxis]];
        if (!isCrossDimDefined) {
          containerCrossAxis = Math.Max(
            // For the cross dim, we add both sides at the end because the value
            // is aggregate via a max function. Intermediate negative values
            // can mess this computation otherwise
            boundAxis(node, crossAxis, crossDim + paddingAndBorderAxisCross),
            paddingAndBorderAxisCross
          );
        }
    
        // <Loop D> Position elements in the cross axis
        for (i = firstComplexCross; i < endLine; ++i) {
          child = node.getChildAt(i);
    
          if (child.style.positionType == CSSPositionType.Absolute &&
              !float.IsNaN(child.style.position[leading[crossAxis]])) {
            // In case the child is absolutely positionned and has a
            // top/left/bottom/right being set, we override all the previously
            // computed positions to set it correctly.
            child.layout.position[pos[crossAxis]] = (float.IsNaN(child.style.position[leading[crossAxis]]) ?  0 : child.style.position[leading[crossAxis]]) +
              node.style.border.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) +
              child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]);
    
          } else {
            float leadingCrossDim = leadingPaddingAndBorderCross;
    
            // For a relative children, we're either using alignItems (parent) or
            // alignSelf (child) in order to determine the position in the cross axis
            if (child.style.positionType == CSSPositionType.Relative) {
              /*eslint-disable */
              // This variable is intentionally re-defined as the code is transpiled to a block scope language
              CSSAlign alignItem = getAlignItem(node, child);
              /*eslint-enable */
              if (alignItem == CSSAlign.Stretch) {
                // You can only stretch if the dimension has not already been set
                // previously.
                if (float.IsNaN(child.layout.dimensions[dim[crossAxis]])) {
                  child.layout.dimensions[dim[crossAxis]] = Math.Max(
                    boundAxis(child, crossAxis, containerCrossAxis -
                      paddingAndBorderAxisCross - (child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]))),
                    // You never want to go smaller than padding
                    ((child.style.padding.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.border.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis])) + (child.style.padding.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]) + child.style.border.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis])))
                  );
                }
              } else if (alignItem != CSSAlign.FlexStart) {
                // The remaining space between the parent dimensions+padding and child
                // dimensions+margin.
                float remainingCrossDim = containerCrossAxis -
                  paddingAndBorderAxisCross - (child.layout.dimensions[dim[crossAxis]] + child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]));
    
                if (alignItem == CSSAlign.Center) {
                  leadingCrossDim += remainingCrossDim / 2;
                } else { // CSSAlign.FlexEnd
                  leadingCrossDim += remainingCrossDim;
                }
              }
            }
    
            // And we apply the position
            child.layout.position[pos[crossAxis]] += linesCrossDim + leadingCrossDim;
    
            // Define the trailing position accordingly.
            if (isCrossDimDefined) {
              child.layout.position[trailing[crossAxis]] = node.layout.dimensions[dim[crossAxis]] - child.layout.dimensions[dim[crossAxis]] - child.layout.position[pos[crossAxis]];
            }
          }
        }
    
        linesCrossDim += crossDim;
        linesMainDim = Math.Max(linesMainDim, mainDim);
        linesCount += 1;
        startLine = endLine;
      }
    
      // <Loop E>
      //
      // Note(prenaux): More than one line, we need to layout the crossAxis
      // according to alignContent.
      //
      // Note that we could probably remove <Loop D> and handle the one line case
      // here too, but for the moment this is safer since it won't interfere with
      // previously working code.
      //
      // See specs:
      // http://www.w3.org/TR/2012/CR-css3-flexbox-20120918/#layout-algorithm
      // section 9.4
      //
      if (linesCount > 1 && isCrossDimDefined) {
        float nodeCrossAxisInnerSize = node.layout.dimensions[dim[crossAxis]] -
            paddingAndBorderAxisCross;
        float remainingAlignContentDim = nodeCrossAxisInnerSize - linesCrossDim;
    
        float crossDimLead = 0;
        float currentLead = leadingPaddingAndBorderCross;
    
        CSSAlign alignContent = node.style.alignContent;
        if (alignContent == CSSAlign.FlexEnd) {
          currentLead += remainingAlignContentDim;
        } else if (alignContent == CSSAlign.Center) {
          currentLead += remainingAlignContentDim / 2;
        } else if (alignContent == CSSAlign.Stretch) {
          if (nodeCrossAxisInnerSize > linesCrossDim) {
            crossDimLead = (remainingAlignContentDim / linesCount);
          }
        }
    
        int endIndex = 0;
        for (i = 0; i < linesCount; ++i) {
          int startIndex = endIndex;
    
          // compute the line's height and find the endIndex
          float lineHeight = 0;
          for (ii = startIndex; ii < childCount; ++ii) {
            child = node.getChildAt(ii);
            if (child.style.positionType != CSSPositionType.Relative) {
              continue;
            }
            if (child.lineIndex != i) {
              break;
            }
            if (!float.IsNaN(child.layout.dimensions[dim[crossAxis]])) {
              lineHeight = Math.Max(
                lineHeight,
                child.layout.dimensions[dim[crossAxis]] + (child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]) + child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]))
              );
            }
          }
          endIndex = ii;
          lineHeight += crossDimLead;
    
          for (ii = startIndex; ii < endIndex; ++ii) {
            child = node.getChildAt(ii);
            if (child.style.positionType != CSSPositionType.Relative) {
              continue;
            }
    
            CSSAlign alignContentAlignItem = getAlignItem(node, child);
            if (alignContentAlignItem == CSSAlign.FlexStart) {
              child.layout.position[pos[crossAxis]] = currentLead + child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]);
            } else if (alignContentAlignItem == CSSAlign.FlexEnd) {
              child.layout.position[pos[crossAxis]] = currentLead + lineHeight - child.style.margin.getWithFallback(trailingSpacing[crossAxis], trailing[crossAxis]) - child.layout.dimensions[dim[crossAxis]];
            } else if (alignContentAlignItem == CSSAlign.Center) {
              float childHeight = child.layout.dimensions[dim[crossAxis]];
              child.layout.position[pos[crossAxis]] = currentLead + (lineHeight - childHeight) / 2;
            } else if (alignContentAlignItem == CSSAlign.Stretch) {
              child.layout.position[pos[crossAxis]] = currentLead + child.style.margin.getWithFallback(leadingSpacing[crossAxis], leading[crossAxis]);
              // TODO(prenaux): Correctly set the height of items with undefined
              //                (auto) crossAxis dimension.
            }
          }
    
          currentLead += lineHeight;
        }
      }
    
      boolean needsMainTrailingPos = false;
      boolean needsCrossTrailingPos = false;
    
      // If the user didn't specify a width or height, and it has not been set
      // by the container, then we set it via the children.
      if (!isMainDimDefined) {
        node.layout.dimensions[dim[mainAxis]] = Math.Max(
          // We're missing the last padding at this point to get the final
          // dimension
          boundAxis(node, mainAxis, linesMainDim + (node.style.padding.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]) + node.style.border.getWithFallback(trailingSpacing[mainAxis], trailing[mainAxis]))),
          // We can never assign a width smaller than the padding and borders
          paddingAndBorderAxisMain
        );
    
        if (mainAxis == CSS_FLEX_DIRECTION_ROW_REVERSE ||
            mainAxis == CSS_FLEX_DIRECTION_COLUMN_REVERSE) {
          needsMainTrailingPos = true;
        }
      }
    
      if (!isCrossDimDefined) {
        node.layout.dimensions[dim[crossAxis]] = Math.Max(
          // For the cross dim, we add both sides at the end because the value
          // is aggregate via a max function. Intermediate negative values
          // can mess this computation otherwise
          boundAxis(node, crossAxis, linesCrossDim + paddingAndBorderAxisCross),
          paddingAndBorderAxisCross
        );
    
        if (crossAxis == CSS_FLEX_DIRECTION_ROW_REVERSE ||
            crossAxis == CSS_FLEX_DIRECTION_COLUMN_REVERSE) {
          needsCrossTrailingPos = true;
        }
      }
    
      // <Loop F> Set trailing position if necessary
      if (needsMainTrailingPos || needsCrossTrailingPos) {
        for (i = 0; i < childCount; ++i) {
          child = node.getChildAt(i);
    
          if (needsMainTrailingPos) {
            child.layout.position[trailing[mainAxis]] = node.layout.dimensions[dim[mainAxis]] - child.layout.dimensions[dim[mainAxis]] - child.layout.position[pos[mainAxis]];
          }
    
          if (needsCrossTrailingPos) {
            child.layout.position[trailing[crossAxis]] = node.layout.dimensions[dim[crossAxis]] - child.layout.dimensions[dim[crossAxis]] - child.layout.position[pos[crossAxis]];
          }
        }
      }
    
      // <Loop G> Calculate dimensions for absolutely positioned elements
      currentAbsoluteChild = firstAbsoluteChild;
      while (currentAbsoluteChild != null) {
        // Pre-fill dimensions when using absolute position and both offsets for
        // the axis are defined (either both left and right or top and bottom).
        for (ii = 0; ii < 2; ii++) {
          axis = (ii != 0) ? CSS_FLEX_DIRECTION_ROW : CSS_FLEX_DIRECTION_COLUMN;
    
          if (!float.IsNaN(node.layout.dimensions[dim[axis]]) &&
              !(!float.IsNaN(currentAbsoluteChild.style.dimensions[dim[axis]]) && currentAbsoluteChild.style.dimensions[dim[axis]] >= 0.0) &&
              !float.IsNaN(currentAbsoluteChild.style.position[leading[axis]]) &&
              !float.IsNaN(currentAbsoluteChild.style.position[trailing[axis]])) {
            currentAbsoluteChild.layout.dimensions[dim[axis]] = Math.Max(
              boundAxis(currentAbsoluteChild, axis, node.layout.dimensions[dim[axis]] -
                (node.style.border.getWithFallback(leadingSpacing[axis], leading[axis]) + node.style.border.getWithFallback(trailingSpacing[axis], trailing[axis])) -
                (currentAbsoluteChild.style.margin.getWithFallback(leadingSpacing[axis], leading[axis]) + currentAbsoluteChild.style.margin.getWithFallback(trailingSpacing[axis], trailing[axis])) -
                (float.IsNaN(currentAbsoluteChild.style.position[leading[axis]]) ?  0 : currentAbsoluteChild.style.position[leading[axis]]) -
                (float.IsNaN(currentAbsoluteChild.style.position[trailing[axis]]) ?  0 : currentAbsoluteChild.style.position[trailing[axis]])
              ),
              // You never want to go smaller than padding
              ((currentAbsoluteChild.style.padding.getWithFallback(leadingSpacing[axis], leading[axis]) + currentAbsoluteChild.style.border.getWithFallback(leadingSpacing[axis], leading[axis])) + (currentAbsoluteChild.style.padding.getWithFallback(trailingSpacing[axis], trailing[axis]) + currentAbsoluteChild.style.border.getWithFallback(trailingSpacing[axis], trailing[axis])))
            );
          }
    
          if (!float.IsNaN(currentAbsoluteChild.style.position[trailing[axis]]) &&
              !!float.IsNaN(currentAbsoluteChild.style.position[leading[axis]])) {
            currentAbsoluteChild.layout.position[leading[axis]] =
              node.layout.dimensions[dim[axis]] -
              currentAbsoluteChild.layout.dimensions[dim[axis]] -
              (float.IsNaN(currentAbsoluteChild.style.position[trailing[axis]]) ?  0 : currentAbsoluteChild.style.position[trailing[axis]]);
          }
        }
    
        child = currentAbsoluteChild;
        currentAbsoluteChild = currentAbsoluteChild.nextAbsoluteChild;
        child.nextAbsoluteChild = null;
      }
    }
  /** END_GENERATED **/
    }
}
