namespace Ayush360.Hms.Api.Consultations;

public sealed record Icd10Entry(string Code, string Description);

public sealed record LabTestEntry(string Code, string Name, string Category);

public sealed record StarterMedicine(
    string Name, string? GenericName, string Strength, string Form, string? DefaultDose, string? DefaultFrequency);

/// <summary>
/// Starter clinical lists, built into the API.
///   - ICD-10: common OPD / casualty diagnoses in India (WHO ICD-10 codes). A full ICD-10 import can replace this
///     later without changing the screens (same search endpoint).
///   - Lab tests and investigations: until the Laboratory module owns its own test master.
///   - Medicines: copied into each hospital's hospital.medicines list, which the hospital (later Pharmacy) maintains.
/// Doctors can always type a diagnosis, test or medicine that is not in these lists.
/// </summary>
public static class ClinicalCatalogs
{
    public static IReadOnlyList<Icd10Entry> SearchIcd10(string? search, int take = 20) =>
        Search(Icd10, search, e => e.Code, e => e.Description, take);

    public static IReadOnlyList<LabTestEntry> SearchLabTests(string? search, int take = 20) =>
        Search(LabTests, search, e => e.Code, e => e.Name, take);

    /// <summary>Code prefix matches first ("J06"), then descriptions containing every typed word ("upper resp").</summary>
    private static IReadOnlyList<T> Search<T>(
        IReadOnlyList<T> items, string? search, Func<T, string> code, Func<T, string> text, int take)
    {
        var term = (search ?? "").Trim();
        if (term.Length == 0)
        {
            return items.Take(take).ToList();
        }

        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var byCode = items.Where(i => code(i).StartsWith(term, StringComparison.OrdinalIgnoreCase));
        var byText = items.Where(i => words.All(w => text(i).Contains(w, StringComparison.OrdinalIgnoreCase)));
        return byCode.Concat(byText).Distinct().Take(take).ToList();
    }

    public static readonly IReadOnlyList<Icd10Entry> Icd10 =
    [
        // Infections
        new("A01.0", "Typhoid fever"),
        new("A09", "Diarrhoea and gastroenteritis of presumed infectious origin"),
        new("A15.0", "Tuberculosis of lung, confirmed by sputum microscopy"),
        new("A16.2", "Tuberculosis of lung, without mention of bacteriological or histological confirmation"),
        new("A27.9", "Leptospirosis, unspecified"),
        new("A41.9", "Sepsis, unspecified"),
        new("A75.3", "Scrub typhus"),
        new("A90", "Dengue fever [classical dengue]"),
        new("A91", "Dengue haemorrhagic fever"),
        new("A92.0", "Chikungunya virus disease"),
        new("B01.9", "Varicella [chickenpox] without complication"),
        new("B02.9", "Zoster [herpes zoster] without complication"),
        new("B05.9", "Measles without complication"),
        new("B07", "Viral warts"),
        new("B15.9", "Hepatitis A without hepatic coma"),
        new("B16.9", "Acute hepatitis B without delta-agent and without hepatic coma"),
        new("B18.1", "Chronic viral hepatitis B without delta-agent"),
        new("B24", "Unspecified human immunodeficiency virus [HIV] disease"),
        new("B34.9", "Viral infection, unspecified"),
        new("B35.4", "Tinea corporis"),
        new("B36.0", "Pityriasis versicolor"),
        new("B37.0", "Candidal stomatitis"),
        new("B50.9", "Plasmodium falciparum malaria, unspecified"),
        new("B54", "Unspecified malaria"),
        new("B86", "Scabies"),
        new("U07.1", "COVID-19, virus identified"),

        // Blood, endocrine, metabolic
        new("D50.9", "Iron deficiency anaemia, unspecified"),
        new("D64.9", "Anaemia, unspecified"),
        new("D69.6", "Thrombocytopenia, unspecified"),
        new("E03.9", "Hypothyroidism, unspecified"),
        new("E05.9", "Thyrotoxicosis, unspecified"),
        new("E10.9", "Type 1 diabetes mellitus without complications"),
        new("E11.9", "Type 2 diabetes mellitus without complications"),
        new("E11.6", "Type 2 diabetes mellitus with other specified complications"),
        new("E16.2", "Hypoglycaemia, unspecified"),
        new("E28.2", "Polycystic ovarian syndrome"),
        new("E53.8", "Deficiency of other specified B group vitamins (vitamin B12)"),
        new("E55.9", "Vitamin D deficiency, unspecified"),
        new("E66.9", "Obesity, unspecified"),
        new("E78.5", "Hyperlipidaemia, unspecified"),
        new("E86", "Volume depletion (dehydration)"),
        new("E87.1", "Hypo-osmolality and hyponatraemia"),
        new("E87.6", "Hypokalaemia"),

        // Mental health, nervous system
        new("F32.9", "Depressive episode, unspecified"),
        new("F41.1", "Generalized anxiety disorder"),
        new("F41.9", "Anxiety disorder, unspecified"),
        new("G40.9", "Epilepsy, unspecified"),
        new("G43.9", "Migraine, unspecified"),
        new("G44.2", "Tension-type headache"),
        new("G47.0", "Disorders of initiating and maintaining sleep [insomnia]"),
        new("G51.0", "Bell palsy"),
        new("G56.0", "Carpal tunnel syndrome"),

        // Eye, ear, nose, throat
        new("H00.0", "Hordeolum and other deep inflammation of eyelid (stye)"),
        new("H10.9", "Conjunctivitis, unspecified"),
        new("H25.9", "Senile cataract, unspecified"),
        new("H40.9", "Glaucoma, unspecified"),
        new("H52.4", "Presbyopia"),
        new("H60.9", "Otitis externa, unspecified"),
        new("H61.2", "Impacted cerumen (ear wax)"),
        new("H66.9", "Otitis media, unspecified"),
        new("H81.1", "Benign paroxysmal vertigo"),
        new("H91.9", "Hearing loss, unspecified"),
        new("H92.0", "Otalgia (ear pain)"),
        new("J34.2", "Deviated nasal septum"),
        new("R04.0", "Epistaxis"),

        // Heart and circulation
        new("I10", "Essential (primary) hypertension"),
        new("I20.9", "Angina pectoris, unspecified"),
        new("I21.9", "Acute myocardial infarction, unspecified"),
        new("I25.1", "Atherosclerotic heart disease"),
        new("I47.1", "Supraventricular tachycardia"),
        new("I48.9", "Atrial fibrillation and atrial flutter, unspecified"),
        new("I50.9", "Heart failure, unspecified"),
        new("I63.9", "Cerebral infarction, unspecified"),
        new("I64", "Stroke, not specified as haemorrhage or infarction"),
        new("I83.9", "Varicose veins of lower extremities without ulcer or inflammation"),
        new("I95.9", "Hypotension, unspecified"),

        // Respiratory
        new("J00", "Acute nasopharyngitis [common cold]"),
        new("J01.9", "Acute sinusitis, unspecified"),
        new("J02.9", "Acute pharyngitis, unspecified"),
        new("J03.9", "Acute tonsillitis, unspecified"),
        new("J04.0", "Acute laryngitis"),
        new("J06.9", "Acute upper respiratory infection, unspecified"),
        new("J11.1", "Influenza with other respiratory manifestations, virus not identified"),
        new("J18.9", "Pneumonia, unspecified"),
        new("J20.9", "Acute bronchitis, unspecified"),
        new("J30.4", "Allergic rhinitis, unspecified"),
        new("J32.9", "Chronic sinusitis, unspecified"),
        new("J35.0", "Chronic tonsillitis"),
        new("J44.9", "Chronic obstructive pulmonary disease, unspecified"),
        new("J45.9", "Asthma, unspecified"),
        new("J96.0", "Acute respiratory failure"),

        // Digestive
        new("K02.9", "Dental caries, unspecified"),
        new("K12.0", "Recurrent oral aphthae (mouth ulcers)"),
        new("K21.9", "Gastro-oesophageal reflux disease without oesophagitis"),
        new("K25.9", "Gastric ulcer, unspecified as acute or chronic, without haemorrhage or perforation"),
        new("K29.7", "Gastritis, unspecified"),
        new("K30", "Functional dyspepsia"),
        new("K35.8", "Acute appendicitis, other and unspecified"),
        new("K40.9", "Unilateral or unspecified inguinal hernia, without obstruction or gangrene"),
        new("K52.9", "Noninfective gastroenteritis and colitis, unspecified"),
        new("K58.9", "Irritable bowel syndrome without diarrhoea"),
        new("K59.0", "Constipation"),
        new("K60.2", "Anal fissure, unspecified"),
        new("K64.9", "Haemorrhoids, unspecified"),
        new("K74.6", "Other and unspecified cirrhosis of liver"),
        new("K76.0", "Fatty (change of) liver, not elsewhere classified"),
        new("K80.2", "Calculus of gallbladder without cholecystitis"),

        // Skin
        new("L01.0", "Impetigo"),
        new("L02.9", "Cutaneous abscess, furuncle and carbuncle, unspecified"),
        new("L03.9", "Cellulitis, unspecified"),
        new("L20.9", "Atopic dermatitis, unspecified"),
        new("L21.9", "Seborrhoeic dermatitis, unspecified"),
        new("L23.9", "Allergic contact dermatitis, unspecified cause"),
        new("L30.9", "Dermatitis, unspecified"),
        new("L40.0", "Psoriasis vulgaris"),
        new("L50.9", "Urticaria, unspecified"),
        new("L60.0", "Ingrowing nail"),
        new("L63.9", "Alopecia areata, unspecified"),
        new("L70.0", "Acne vulgaris"),
        new("L80", "Vitiligo"),

        // Musculoskeletal
        new("M06.9", "Rheumatoid arthritis, unspecified"),
        new("M10.9", "Gout, unspecified"),
        new("M17.9", "Gonarthrosis, unspecified (osteoarthritis of knee)"),
        new("M19.9", "Arthrosis, unspecified"),
        new("M25.5", "Pain in joint"),
        new("M47.9", "Spondylosis, unspecified"),
        new("M51.2", "Other specified intervertebral disc displacement"),
        new("M54.2", "Cervicalgia (neck pain)"),
        new("M54.3", "Sciatica"),
        new("M54.4", "Lumbago with sciatica"),
        new("M54.5", "Low back pain"),
        new("M62.6", "Muscle strain"),
        new("M65.3", "Trigger finger"),
        new("M72.2", "Plantar fascial fibromatosis (plantar fasciitis)"),
        new("M75.0", "Adhesive capsulitis of shoulder (frozen shoulder)"),
        new("M77.1", "Lateral epicondylitis (tennis elbow)"),
        new("M79.1", "Myalgia"),
        new("M79.6", "Pain in limb"),
        new("M81.9", "Osteoporosis, unspecified"),

        // Kidney, urinary, reproductive, pregnancy
        new("N10", "Acute tubulo-interstitial nephritis (acute pyelonephritis)"),
        new("N18.9", "Chronic kidney disease, unspecified"),
        new("N20.0", "Calculus of kidney"),
        new("N30.0", "Acute cystitis"),
        new("N39.0", "Urinary tract infection, site not specified"),
        new("N40", "Hyperplasia of prostate"),
        new("N45.9", "Orchitis, epididymitis and epididymo-orchitis without abscess"),
        new("N63", "Unspecified lump in breast"),
        new("N76.0", "Acute vaginitis"),
        new("N91.2", "Amenorrhoea, unspecified"),
        new("N92.0", "Excessive and frequent menstruation with regular cycle"),
        new("N94.6", "Dysmenorrhoea, unspecified"),
        new("O21.0", "Mild hyperemesis gravidarum"),
        new("Z34.9", "Supervision of normal pregnancy, unspecified"),
        new("P59.9", "Neonatal jaundice, unspecified"),

        // Symptoms and signs
        new("R05", "Cough"),
        new("R06.0", "Dyspnoea"),
        new("R07.4", "Chest pain, unspecified"),
        new("R10.4", "Other and unspecified abdominal pain"),
        new("R11", "Nausea and vomiting"),
        new("R31", "Unspecified haematuria"),
        new("R42", "Dizziness and giddiness"),
        new("R50.9", "Fever, unspecified"),
        new("R51", "Headache"),
        new("R53", "Malaise and fatigue"),
        new("R55", "Syncope and collapse"),
        new("R59.0", "Localized enlarged lymph nodes"),
        new("R60.0", "Localized oedema"),
        new("R63.4", "Abnormal weight loss"),
        new("R73.9", "Hyperglycaemia, unspecified"),

        // Injuries, poisoning, allergy
        new("S06.0", "Concussion"),
        new("S09.9", "Unspecified injury of head"),
        new("S52.5", "Fracture of lower end of radius"),
        new("S62.6", "Fracture of other finger"),
        new("S83.6", "Sprain and strain of other and unspecified parts of knee"),
        new("S93.4", "Sprain and strain of ankle"),
        new("T14.0", "Superficial injury of unspecified body region"),
        new("T14.1", "Open wound of unspecified body region"),
        new("T30.0", "Burn of unspecified body region, unspecified degree"),
        new("T60.0", "Toxic effect of organophosphate and carbamate insecticides"),
        new("T63.0", "Toxic effect of snake venom"),
        new("T78.4", "Allergy, unspecified"),

        // Check-ups and follow-up
        new("Z00.0", "General medical examination"),
        new("Z09.9", "Follow-up examination after unspecified treatment for other conditions"),
        new("Z30.0", "General counselling and advice on contraception"),
    ];

    public static readonly IReadOnlyList<LabTestEntry> LabTests =
    [
        // Haematology
        new("CBC", "Complete blood count (CBC)", "Haematology"),
        new("ESR", "Erythrocyte sedimentation rate (ESR)", "Haematology"),
        new("PS", "Peripheral smear", "Haematology"),
        new("BGRH", "Blood group and Rh type", "Haematology"),
        new("PTINR", "Prothrombin time / INR", "Haematology"),
        // Biochemistry
        new("FBS", "Blood sugar - fasting (FBS)", "Biochemistry"),
        new("PPBS", "Blood sugar - post prandial (PPBS)", "Biochemistry"),
        new("RBS", "Blood sugar - random (RBS)", "Biochemistry"),
        new("HBA1C", "HbA1c (glycated haemoglobin)", "Biochemistry"),
        new("LIPID", "Lipid profile", "Biochemistry"),
        new("LFT", "Liver function test (LFT)", "Biochemistry"),
        new("RFT", "Renal function test (RFT / KFT)", "Biochemistry"),
        new("CREAT", "Serum creatinine", "Biochemistry"),
        new("UREA", "Blood urea", "Biochemistry"),
        new("ELEC", "Serum electrolytes (Na, K, Cl)", "Biochemistry"),
        new("URIC", "Serum uric acid", "Biochemistry"),
        new("CRP", "C-reactive protein (CRP)", "Biochemistry"),
        new("TFT", "Thyroid profile (T3, T4, TSH)", "Biochemistry"),
        new("TSH", "TSH", "Biochemistry"),
        new("VITD", "Vitamin D (25-OH)", "Biochemistry"),
        new("VITB12", "Vitamin B12", "Biochemistry"),
        new("IRON", "Iron studies (serum iron, TIBC, ferritin)", "Biochemistry"),
        new("TROP", "Troponin I", "Biochemistry"),
        new("BHCG", "Serum beta hCG", "Biochemistry"),
        // Serology / infections
        new("NS1", "Dengue NS1 antigen", "Serology"),
        new("DENGAB", "Dengue IgM / IgG antibodies", "Serology"),
        new("MPRDT", "Malaria antigen (rapid test)", "Serology"),
        new("WIDAL", "Widal test", "Serology"),
        new("TYPHIDOT", "Typhidot IgM", "Serology"),
        new("HIV", "HIV 1 & 2 antibodies", "Serology"),
        new("HBSAG", "HBsAg", "Serology"),
        new("HCV", "Anti-HCV antibodies", "Serology"),
        new("VDRL", "VDRL", "Serology"),
        // Urine, stool, cultures
        new("URINE", "Urine routine and microscopy", "Clinical pathology"),
        new("UPT", "Urine pregnancy test", "Clinical pathology"),
        new("STOOL", "Stool routine and microscopy", "Clinical pathology"),
        new("UCS", "Urine culture and sensitivity", "Microbiology"),
        new("BCS", "Blood culture and sensitivity", "Microbiology"),
        new("AFB", "Sputum for AFB", "Microbiology"),
        // Cardiology / imaging
        new("ECG", "ECG (12 lead)", "Cardiology"),
        new("ECHO", "2D Echocardiography", "Cardiology"),
        new("XRCHEST", "X-ray chest PA view", "Radiology"),
        new("XRAY", "X-ray (specify part in notes)", "Radiology"),
        new("USGABD", "Ultrasound abdomen", "Radiology"),
        new("USGPEL", "Ultrasound pelvis", "Radiology"),
        new("CTBRAIN", "CT brain (plain)", "Radiology"),
    ];

    /// <summary>Copied into a hospital's medicine list when it starts (demo hospitals: on first run).</summary>
    public static readonly IReadOnlyList<StarterMedicine> StarterMedicines =
    [
        // Fever, pain
        new("Paracetamol", "Paracetamol", "500 mg", "Tablet", "1 tablet", "1-1-1"),
        new("Paracetamol", "Paracetamol", "650 mg", "Tablet", "1 tablet", "1-1-1"),
        new("Paracetamol", "Paracetamol", "250 mg/5 ml", "Syrup", "5 ml", "1-1-1"),
        new("Ibuprofen", "Ibuprofen", "400 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Diclofenac", "Diclofenac sodium", "50 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Aceclofenac + Paracetamol", "Aceclofenac + Paracetamol", "100 mg + 325 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Diclofenac", "Diclofenac diethylamine", "1%", "Gel", "Apply locally", "1-1-1"),
        new("Thiocolchicoside", "Thiocolchicoside", "4 mg", "Capsule", "1 capsule", "1-0-1"),
        // Allergy, cough, cold
        new("Cetirizine", "Cetirizine", "10 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Cetirizine", "Cetirizine", "5 mg/5 ml", "Syrup", "5 ml", "0-0-1"),
        new("Levocetirizine", "Levocetirizine", "5 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Montelukast + Levocetirizine", "Montelukast + Levocetirizine", "10 mg + 5 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Fexofenadine", "Fexofenadine", "120 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Ambroxol", "Ambroxol", "30 mg/5 ml", "Syrup", "5 ml", "1-1-1"),
        new("Xylometazoline", "Xylometazoline", "0.1%", "Nasal drops", "2 drops each nostril", "1-0-1"),
        new("Salbutamol", "Salbutamol", "100 mcg/puff", "Inhaler", "2 puffs", "SOS"),
        new("Budesonide + Formoterol", "Budesonide + Formoterol", "200 mcg + 6 mcg", "Inhaler", "1 puff", "1-0-1"),
        // Antibiotics, anti-infectives
        new("Amoxicillin", "Amoxicillin", "500 mg", "Capsule", "1 capsule", "1-1-1"),
        new("Amoxicillin", "Amoxicillin", "125 mg/5 ml", "Syrup", "5 ml", "1-1-1"),
        new("Amoxicillin + Clavulanic acid", "Amoxicillin + Clavulanic acid", "625 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Azithromycin", "Azithromycin", "500 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Cefixime", "Cefixime", "200 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Ciprofloxacin", "Ciprofloxacin", "500 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Ofloxacin", "Ofloxacin", "200 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Doxycycline", "Doxycycline", "100 mg", "Capsule", "1 capsule", "1-0-1"),
        new("Metronidazole", "Metronidazole", "400 mg", "Tablet", "1 tablet", "1-1-1"),
        new("Nitrofurantoin", "Nitrofurantoin", "100 mg", "Capsule", "1 capsule", "1-0-1"),
        new("Fluconazole", "Fluconazole", "150 mg", "Tablet", "1 tablet", "Once a week"),
        new("Albendazole", "Albendazole", "400 mg", "Tablet", "1 tablet", "STAT"),
        new("Ivermectin", "Ivermectin", "12 mg", "Tablet", "1 tablet", "STAT"),
        new("Clotrimazole", "Clotrimazole", "1%", "Cream", "Apply locally", "1-0-1"),
        new("Mupirocin", "Mupirocin", "2%", "Ointment", "Apply locally", "1-0-1"),
        new("Permethrin", "Permethrin", "5%", "Cream", "Apply whole body", "0-0-1"),
        new("Ciprofloxacin", "Ciprofloxacin", "0.3%", "Eye drops", "1 drop", "1-1-1-1"),
        // Stomach
        new("Pantoprazole", "Pantoprazole", "40 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Omeprazole", "Omeprazole", "20 mg", "Capsule", "1 capsule", "1-0-0"),
        new("Rabeprazole + Domperidone", "Rabeprazole + Domperidone", "20 mg + 30 mg", "Capsule", "1 capsule", "1-0-0"),
        new("Ondansetron", "Ondansetron", "4 mg", "Tablet", "1 tablet", "SOS"),
        new("Domperidone", "Domperidone", "10 mg", "Tablet", "1 tablet", "1-1-1"),
        new("Dicyclomine", "Dicyclomine", "10 mg", "Tablet", "1 tablet", "SOS"),
        new("Loperamide", "Loperamide", "2 mg", "Capsule", "1 capsule", "SOS"),
        new("ORS", "Oral rehydration salts", "21 g", "Sachet", "1 sachet in 1 litre water", "SOS"),
        new("Lactulose", "Lactulose", "10 g/15 ml", "Syrup", "15 ml", "0-0-1"),
        // Diabetes, BP, heart, thyroid
        new("Metformin", "Metformin", "500 mg", "Tablet", "1 tablet", "1-0-1"),
        new("Glimepiride", "Glimepiride", "1 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Glimepiride", "Glimepiride", "2 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Amlodipine", "Amlodipine", "5 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Telmisartan", "Telmisartan", "40 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Losartan", "Losartan", "50 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Atenolol", "Atenolol", "50 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Metoprolol succinate", "Metoprolol succinate", "25 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Atorvastatin", "Atorvastatin", "10 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Atorvastatin", "Atorvastatin", "20 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Rosuvastatin", "Rosuvastatin", "10 mg", "Tablet", "1 tablet", "0-0-1"),
        new("Aspirin", "Acetylsalicylic acid", "75 mg", "Tablet", "1 tablet", "0-1-0"),
        new("Clopidogrel", "Clopidogrel", "75 mg", "Tablet", "1 tablet", "0-1-0"),
        new("Levothyroxine", "Levothyroxine", "50 mcg", "Tablet", "1 tablet", "1-0-0"),
        new("Prednisolone", "Prednisolone", "10 mg", "Tablet", "1 tablet", "1-0-0"),
        // Vitamins, supplements, eye
        new("Vitamin D3", "Cholecalciferol", "60000 IU", "Capsule", "1 capsule", "Once a week"),
        new("Calcium + Vitamin D3", "Calcium carbonate + Cholecalciferol", "500 mg + 250 IU", "Tablet", "1 tablet", "0-1-0"),
        new("Ferrous ascorbate + Folic acid", "Ferrous ascorbate + Folic acid", "100 mg + 1.5 mg", "Tablet", "1 tablet", "0-1-0"),
        new("Folic acid", "Folic acid", "5 mg", "Tablet", "1 tablet", "1-0-0"),
        new("Methylcobalamin", "Methylcobalamin", "1500 mcg", "Tablet", "1 tablet", "1-0-0"),
        new("Multivitamin", "Multivitamin and minerals", "-", "Tablet", "1 tablet", "0-1-0"),
        new("Carboxymethylcellulose", "Carboxymethylcellulose", "0.5%", "Eye drops", "1 drop", "1-1-1-1"),
    ];
}
