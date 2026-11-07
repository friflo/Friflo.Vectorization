// timer.js
// noinspection JSUnusedGlobalSymbols

// Interval for 60 Hz (1000ms / 60 ≈ 16.66ms)
const interval = 1000 / 60;
let expected = 0;
let timerId = null;

self.onmessage = function (e) {
  if (e.data === 'startTimer') {
    startTimer();
  } else if (e.data === 'stopTimer') {
    stopTimer();
  }
};

function startTimer() {
  if (timerId) return;
  expected = performance.now() + interval;
  timerId = setTimeout(step, interval);
}

function stopTimer() {
  clearTimeout(timerId);
  timerId = null;
}

function step() {
  const drift = performance.now() - expected;
  
  // Notify main thread about the tick
  self.postMessage('tick');
  
  expected += interval;
  // Adjust timeout to compensate for execution drift
  timerId = setTimeout(step, Math.max(0, interval - drift));
}