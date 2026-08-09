export type NavLink = { label: string; href: string };

// Root-relative so the nav works from any route (e.g. /book), not just the homepage.
// A bare "#services" would resolve against the current path and go nowhere off "/".
export const navLinks: NavLink[] = [
  { label: "Home", href: "/#home" },
  { label: "Services", href: "/#services" },
  { label: "Products", href: "/products" },
  { label: "Gallery", href: "/#gallery" },
  { label: "Team", href: "/#team" },
  { label: "Reviews", href: "/#reviews" },
  { label: "Contact", href: "/#contact" },
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
    name: "Zin Min",
    role: "Founder & Master Stylist",
    bio: "Leading Zach Hair Studio with 18 years of expertise in the hair industry.",
    avatar:
      "bg-gradient-to-br from-gold/30 to-gold/5 border-gold/30 group-hover:border-gold",
    iconColor: "text-gold/70",
    status: "green",
  },
  {
    name: "May Yoon",
    role: "Color Specialist",
    bio: "Expert in balayage, ombre, and vivid color transformations.",
    avatar:
      "bg-gradient-to-br from-rose-900/40 to-charcoal border-white/10 group-hover:border-gold",
    iconColor: "text-rose-400/70",
    status: "green",
  },
  {
    name: "Thiri Cho",
    role: "Texture & Curl Expert",
    bio: "Specializing in natural hair, curls, and protective styling.",
    avatar:
      "bg-gradient-to-br from-sky-900/40 to-charcoal border-white/10 group-hover:border-gold",
    iconColor: "text-sky-400/70",
    status: "yellow",
  },
  {
    name: "Sai Min Htet",
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
