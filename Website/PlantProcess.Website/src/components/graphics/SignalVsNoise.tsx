export function SignalVsNoise() {
  return (
    <div className="signal-chart" role="img" aria-labelledby="signal-chart-title signal-chart-desc">
      <span id="signal-chart-title" className="sr-only">Signal versus null-control comparison</span>
      <span id="signal-chart-desc" className="sr-only">
        The CRACK_LONG validation relation is 9.3 times while the SCRATCH null control remains at 1.0 times, demonstrating separation of signal from noise.
      </span>

      <div className="signal-chart__plot" aria-hidden="true">
        <div className="signal-chart__gridlines">
          {Array.from({ length: 6 }).map((_, index) => <span key={index} />)}
        </div>

        <div className="signal-chart__axis">
          <span>10x</span><span>8x</span><span>6x</span><span>4x</span><span>2x</span><span>0</span>
        </div>

        <div className="signal-chart__bars">
          <div className="signal-bar signal-bar--supported">
            <div className="signal-bar__value">9.3x</div>
            <div className="signal-bar__column" />
            <strong>CRACK_LONG</strong>
            <small>Evidence-supported signal</small>
          </div>
          <div className="signal-bar signal-bar--null">
            <div className="signal-bar__value">1.0x</div>
            <div className="signal-bar__column" />
            <strong>SCRATCH</strong>
            <small>Null control stayed neutral</small>
          </div>
        </div>
      </div>

      <div className="signal-chart__evidence" aria-hidden="true">
        <span><b>Population</b> stated</span>
        <span><b>Method</b> visible</span>
        <span><b>FDR</b> controlled</span>
        <span><b>Run</b> traceable</span>
      </div>
    </div>
  );
}

export default SignalVsNoise;
