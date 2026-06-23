export type NavLink = { label: string; href: string };

export const navLinks: NavLink[] = [
  { label: "Home", href: "#home" },
  { label: "Services", href: "#services" },
  { label: "Gallery", href: "#gallery" },
  { label: "Team", href: "#team" },
  { label: "Reviews", href: "#reviews" },
  { label: "Contact", href: "#contact" },
];

export type Service = {
  title: string;
  description: string;
  price: string;
  /** SVG path data for the service icon */
  icon: string;
};

export const services: Service[] = [
  {
    title: "Precision Cut",
    description:
      "Tailored haircuts designed to complement your face shape and lifestyle perfectly.",
    price: "$35",
    icon: "M14.121 14.121L19 19m-7-7l7-7m-7 7l-2.879 2.879M12 12L9.121 9.121m0 5.758a3 3 0 10-4.243 4.243 3 3 0 004.243-4.243zm0-5.758a3 3 0 10-4.243-4.243 3 3 0 004.243 4.243z",
  },
  {
    title: "Color & Highlights",
    description:
      "Vibrant color treatments and natural-looking highlights using premium products.",
    price: "$80",
    icon: "M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01",
  },
  {
    title: "Blowout & Styling",
    description:
      "Professional blowouts and styling for any occasion — weddings, events, or everyday glam.",
    price: "$55",
    icon: "M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z",
  },
  {
    title: "Keratin Treatment",
    description:
      "Smoothing treatments that eliminate frizz and add lasting shine and manageability.",
    price: "$120",
    icon: "M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z",
  },
  {
    title: "Scalp Treatment",
    description:
      "Revitalizing scalp therapies to promote health, hydration, and hair growth.",
    price: "$65",
    icon: "M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z",
  },
];

export type GalleryItem = {
  src: string;
  alt: string;
  title: string;
  tag: string;
  /** object-position class for the image */
  position: string;
};

export const galleryItems: GalleryItem[] = [
  {
    src: "/hair_color_1.jpg",
    alt: "Blue & Yellow Two-Tone",
    title: "Two-Tone Color Cut",
    tag: "Blue & Yellow",
    position: "object-center",
  },
  {
    src: "/hair_color_2.jpg",
    alt: "Ash Grey Long Straight",
    title: "Ash Grey Straight",
    tag: "Silver Tone",
    position: "object-center",
  },
  {
    src: "/hair_color_3.jpg",
    alt: "Vivid Purple Waves",
    title: "Vivid Purple Waves",
    tag: "Bold Color",
    position: "object-center",
  },
  {
    src: "/hair_color_4.jpg",
    alt: "Fiery Orange Long Curls",
    title: "Fiery Orange Curls",
    tag: "Vivid Color Melt",
    position: "object-top",
  },
  {
    src: "/hair_color_5.jpg",
    alt: "Mauve Pink Straight",
    title: "Mauve Pink Straight",
    tag: "Soft Color & Sleek Finish",
    position: "object-top",
  },
  {
    src: "/hair_color_6.jpg",
    alt: "Signature Style",
    title: "Signature Style",
    tag: "Salon Exclusive",
    position: "object-center",
  },
];

export type TeamMember = {
  name: string;
  role: string;
  bio: string;
  /** tailwind gradient + ring classes for the avatar */
  avatar: string;
  iconColor: string;
  status: "green" | "yellow";
};

export const team: TeamMember[] = [
  {
    name: "Mr. Zachary",
    role: "Founder & Master Stylist",
    bio: "Leading Zach Hair Studio with 18 years of expertise in the hair industry.",
    avatar:
      "bg-gradient-to-br from-gold/30 to-gold/5 border-gold/30 group-hover:border-gold",
    iconColor: "text-gold/70",
    status: "green",
  },
  {
    name: "Aria Chen",
    role: "Color Specialist",
    bio: "Expert in balayage, ombre, and vivid color transformations.",
    avatar:
      "bg-gradient-to-br from-rose-900/40 to-charcoal border-white/10 group-hover:border-gold",
    iconColor: "text-rose-400/70",
    status: "green",
  },
  {
    name: "Marcus Lee",
    role: "Texture & Curl Expert",
    bio: "Specializing in natural hair, curls, and protective styling.",
    avatar:
      "bg-gradient-to-br from-sky-900/40 to-charcoal border-white/10 group-hover:border-gold",
    iconColor: "text-sky-400/70",
    status: "yellow",
  },
  {
    name: "Sofia Reyes",
    role: "Bridal & Event Stylist",
    bio: "Creating unforgettable looks for weddings and special occasions.",
    avatar:
      "bg-gradient-to-br from-purple-900/40 to-charcoal border-white/10 group-hover:border-gold",
    iconColor: "text-purple-400/70",
    status: "green",
  },
];

export type Review = {
  quote: string;
  name: string;
  role: string;
  initial: string;
  avatar: string;
  featured?: boolean;
};

export const reviews: Review[] = [
  {
    quote:
      "Zach completely transformed my look! He treated my hair and gave me the exact haircut I wanted — it turned out absolutely beautiful.",
    name: "Khattar",
    role: "Happy Client",
    initial: "K",
    avatar: "bg-gradient-to-br from-amber-600 to-amber-800",
  },
  {
    quote:
      "လက်ရာကောင်းပေမယ့် ဈေးလည်းအရမ်းမကြီးဘူး။ ညှပ်၊ လျှော်ကို ၂ သောင်းမကျော်ဘူးဆိုတော့ ဒီဈေးနဲ့ ဒီလိုမျိုး quality cut နဲ့ကအဆင်ပြေသလားလို့လေ။ Highly recommended ပါ။",
    name: "Rosie",
    role: "Recommended Client",
    initial: "R",
    avatar: "bg-gradient-to-br from-emerald-600 to-emerald-800",
    featured: true,
  },
  {
    quote: "Favorite hair studio in town 💇",
    name: "Paing Thet Htar",
    role: "Loyal Client",
    initial: "P",
    avatar: "bg-gradient-to-br from-rose-600 to-rose-800",
  },
];

export const serviceOptions = [
  { value: "cut", label: "Precision Cut – $35" },
  { value: "color", label: "Color & Highlights – $80" },
  { value: "blowout", label: "Blowout & Styling – $55" },
  { value: "keratin", label: "Keratin Treatment – $120" },
  { value: "scalp", label: "Scalp Treatment – $65" },
  { value: "package", label: "Full Glam Package – $199" },
];

export const branches = [
  {
    name: "Zach Hair Studio (1)",
    address: ["အမှတ် ၂၁၂၊ ပုဇွန်တောင်မြို့နယ်၊", "ဗိုလ်တထောင်ဘုရားလမ်း၊ ရန်ကုန်မြို့။"],
    phone: { display: "09-777 190 314", tel: "+959777190314" },
  },
  {
    name: "Zach Hair Studio (2)",
    address: ["အမှတ် (၁၃၂)၊ ဗားကရာလမ်းမကြီး၊", "စမ်းချောင်းမြို့နယ်၊ ရန်ကုန်မြို့။"],
    phone: { display: "09-753 011 309", tel: "+9509753011309" },
  },
];

export const contactEmail = "aprileisu.2019@gmail.com";
