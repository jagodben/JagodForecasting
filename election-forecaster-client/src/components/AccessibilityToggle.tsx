import { useAccessibility } from '../context/AccessibilityContext';

// Inline switch (lives in the site footer) that turns the colorblind-friendly map patterns on/off.
export const AccessibilityToggle = () => {
  const { patterns, toggle } = useAccessibility();

  return (
    <button
      onClick={toggle}
      role="switch"
      aria-checked={patterns}
      aria-label="Colorblind-friendly patterns"
      title="Colorblind-friendly map patterns"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        padding: 0,
        border: 'none',
        outline: 'none',
        background: 'transparent',
        cursor: 'pointer',
        fontSize: 12,
        fontWeight: 400,
        fontFamily: 'inherit',
        textTransform: 'none',
        letterSpacing: 'normal',
        color: 'rgba(255,255,255,0.6)',
      }}
    >
      <span className="accessibility-toggle__label">Accessibility</span>
      <span
        style={{
          width: 26,
          height: 15,
          borderRadius: 999,
          background: patterns ? '#6366f1' : 'rgba(255,255,255,0.3)',
          position: 'relative',
          transition: 'background 0.2s ease',
          flexShrink: 0,
        }}
      >
        <span
          style={{
            position: 'absolute',
            top: 2,
            left: patterns ? 13 : 2,
            width: 11,
            height: 11,
            borderRadius: '50%',
            background: '#ffffff',
            transition: 'left 0.2s ease',
          }}
        />
      </span>
    </button>
  );
};
