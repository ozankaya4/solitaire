// Hand-drawn SVG icon set. No emoji, no icon library. Every icon inherits
// `currentColor` and takes a `size`; decorative by default (aria-hidden), with
// an optional `title` for standalone semantic use.

import type { ReactNode, SVGProps } from 'react';

export interface IconProps extends Omit<SVGProps<SVGSVGElement>, 'children'> {
  size?: number;
  title?: string;
}

function Svg({ size = 24, title, children, ...rest }: IconProps & { children: ReactNode }) {
  const a11y = title !== undefined ? { role: 'img' as const } : { 'aria-hidden': true as const };
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      focusable="false"
      {...a11y}
      {...rest}
    >
      {title !== undefined ? <title>{title}</title> : null}
      {children}
    </svg>
  );
}

export function SpadeIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M12 2.5C12 2.5 4.5 8 4.5 13a4 4 0 0 0 6.2 3.35C10.4 18 9.6 19.4 8 20.2V21h8v-.8c-1.6-.8-2.4-2.2-2.7-3.85A4 4 0 0 0 19.5 13c0-5-7.5-10.5-7.5-10.5Z"
        fill="currentColor"
      />
    </Svg>
  );
}

export function HeartIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M12 20.5S3.5 15 3.5 8.8A4.3 4.3 0 0 1 12 6.6a4.3 4.3 0 0 1 8.5 2.2C20.5 15 12 20.5 12 20.5Z"
        fill="currentColor"
      />
    </Svg>
  );
}

export function DiamondIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M12 2.5 20 12l-8 9.5L4 12 12 2.5Z" fill="currentColor" />
    </Svg>
  );
}

export function ClubIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M12 2.6a3.4 3.4 0 0 0-2.6 5.6 3.4 3.4 0 1 0-1.2 6.35A3.4 3.4 0 0 0 11 13.6c-.1 1.9-.9 3.6-2.6 4.6V21h7.2v-2.8c-1.7-1-2.5-2.7-2.6-4.6a3.4 3.4 0 0 0 2.8 1a3.4 3.4 0 1 0-1.2-6.4A3.4 3.4 0 0 0 12 2.6Z"
        fill="currentColor"
      />
    </Svg>
  );
}

// A stacked-cards logo mark.
export function CardsIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect
        x="7.5"
        y="4"
        width="11"
        height="15"
        rx="1.6"
        transform="rotate(9 13 11.5)"
        stroke="currentColor"
        strokeWidth="1.6"
      />
      <rect
        x="4"
        y="5"
        width="11"
        height="15"
        rx="1.6"
        transform="rotate(-7 9.5 12.5)"
        fill="var(--color-surface)"
        stroke="currentColor"
        strokeWidth="1.6"
      />
      <path d="M9.6 15.3 8 13.4l1.6-1.9 1.6 1.9-1.6 1.9Z" fill="currentColor" />
    </Svg>
  );
}

export function GearIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M12 3.2l1.5.9 1.7-.4.9 1.5 1.6.7.1 1.8 1.3 1.2-.5 1.7 .5 1.7-1.3 1.2-.1 1.8-1.6.7-.9 1.5-1.7-.4-1.5.9-1.5-.9-1.7.4-.9-1.5-1.6-.7-.1-1.8-1.3-1.2.5-1.7-.5-1.7 1.3-1.2.1-1.8 1.6-.7.9-1.5 1.7.4L12 3.2Z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.5" />
    </Svg>
  );
}

export function MoonIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M20 14.2A8 8 0 1 1 9.8 4 6.4 6.4 0 0 0 20 14.2Z"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function SunIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="1.6" />
      <path
        d="M12 2.5v2.2M12 19.3v2.2M4.4 4.4l1.6 1.6M18 18l1.6 1.6M2.5 12h2.2M19.3 12h2.2M4.4 19.6l1.6-1.6M18 6l1.6-1.6"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
      />
    </Svg>
  );
}

export function GlobeIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M3 12h18M12 3c2.6 2.4 2.6 15.6 0 18M12 3c-2.6 2.4-2.6 15.6 0 18"
        stroke="currentColor"
        strokeWidth="1.5"
      />
    </Svg>
  );
}

export function BookmarkStackIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M6 4h9a2 2 0 0 1 2 2v13l-6.5-3.6L4 19V6"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
      <path
        d="M8 2.5h9a2 2 0 0 1 2 2v10"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        opacity="0.5"
      />
    </Svg>
  );
}

export function PlayIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M7 4.5 19 12 7 19.5v-15Z" fill="currentColor" />
    </Svg>
  );
}

export function ChevronDownIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="m5.5 9 6.5 6 6.5-6"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function ArrowLeftIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M11 5.5 4.5 12 11 18.5M4.8 12H20"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function CheckIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="m4.5 12.5 4.5 4.5 10.5-11"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}

export function StarIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path
        d="M12 3.5 14 9l5.8.2-4.6 3.5 1.6 5.6L12 15.1 7.2 18.3l1.6-5.6L4.2 9.2 10 9l2-5.5Z"
        fill="currentColor"
      />
    </Svg>
  );
}
