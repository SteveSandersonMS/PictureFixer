export function init(container) {
    const img = container.querySelector('img');
    if (img.src && img.complete) {
        attachPaintArea(img);
    } else {
        img.onload = () => attachPaintArea(img);
    }
}

export function clearSelection(container) {
    const canvas = container.querySelector('canvas');
    const ctx = canvas.ctx;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
}

function attachPaintArea(img) {
    img.onload = null;
    const canvas = document.createElement('canvas');
    canvas.width = img.width;
    canvas.height = img.height;
    img.parentNode.appendChild(canvas);
    canvas.ctx = canvas.getContext('2d');
    canvas.ctx.sourceImage = img;

    canvas.addEventListener('mousedown', onDrawStart);
    canvas.addEventListener('mousemove', onDrawMove);
    canvas.addEventListener('mouseup', onDrawEnd);
}

function onDrawStart(evt) {
    const canvas = this;
    canvas.width = canvas.ctx.sourceImage.width;
    canvas.height = canvas.ctx.sourceImage.height;
    canvas.currentPath = [{ x: evt.offsetX, y: evt.offsetY }];
}

function onDrawMove(evt) {
    const canvas = this;
    if (!canvas.currentPath)
        return;
    const newPoint = { x: evt.offsetX, y: evt.offsetY };
    const prevPoint = canvas.currentPath[canvas.currentPath.length - 1];
    const distanceSquared = (newPoint.x - prevPoint.x) * (newPoint.x - prevPoint.x) + (newPoint.y - prevPoint.y) * (newPoint.y - prevPoint.y);
    if (distanceSquared > 9) {
        canvas.currentPath.push(newPoint);
        drawCurrentPath(canvas);
    }
}

async function onDrawEnd(evt) {
    const canvas = this;
    const ctx = canvas.ctx;

    // Get source image bytes
    ctx.drawImage(ctx.sourceImage, 0, 0, ctx.sourceImage.width, ctx.sourceImage.height);
    const imageArrayBuffer = await getCanvasDataAsync(canvas);

    // Get mask image bytes
    drawCurrentPath(canvas);
    const selectionArrayBuffer = await getCanvasDataAsync(canvas);
    canvas.currentPath = null;

    // Raise custom event
    canvas.dispatchEvent(new CustomEvent('regiondrawn', {
        bubbles: true,
        detail: {
            // These properties are JSON-serialized and become the event args on the .NET side
            sourceImage: DotNet.createJSObjectReference(readable(imageArrayBuffer)),
            selectedRegion: DotNet.createJSObjectReference(readable(selectionArrayBuffer)),
        }
    }));
}

function getCanvasDataAsync(canvas) {
    return new Promise(resolve => {
        canvas.toBlob(result => resolve(result.arrayBuffer()), 'image/png');
    });
}

function drawCurrentPath(canvas) {
    const ctx = canvas.ctx;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.beginPath();
    ctx.fillStyle = 'rgba(255,0,0,0.4)';
    ctx.moveTo(canvas.currentPath[0].x, canvas.currentPath[0].y);
    for (var i = 1; i < canvas.currentPath.length; i++) {
        ctx.lineTo(canvas.currentPath[i].x, canvas.currentPath[i].y);
    }
    ctx.closePath();
    ctx.fill();

    ctx.setLineDash([3, 6]);
    ctx.strokeStyle = 'black';
    ctx.stroke();
}

function readable(arrayBuffer) {
    return {
        getBytes: () => BINDING.js_typed_array_to_array(new Uint8Array(arrayBuffer))
    };
}

Blazor.registerCustomEventType('regiondrawn', { createEventArgs: event => event.detail });
