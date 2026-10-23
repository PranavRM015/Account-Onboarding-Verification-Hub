export interface CustomerDetails {
  firstName: string;
  middleName: string | null;
  lastName: string;
  dateOfBirth: string;
  gender: string;
  taxIdType: string;
  residentialAddress: string;
  countryOfResidence: string;
  countryOfCitizenship: string;
  occupation: string;
  employer: string;
}

export interface TicketFlag {
  flagId: number;
  violationType: string;
  raisedAt: string;
  cleared: boolean;
  clearedAt: string | null;
  clearedBy: string | null;
  clearedReason: string | null;
}

export interface VendorResponseDetails {
  nameMatch: string;
  dateOfBirthMatch: string;
  addressMatch: string;
  taxIdMatch: string;
  score: number;
  riskCodes: string;
  vrTimeStamp: string;
}

export interface DecisionDetails {
  decisionType: string;
  decidedBy: string;
  reason: string;
  decidedAt: string;
}

export interface TicketDetails {
  ticketId: number;
  customerId: number;
  ticketStatus: string;
  riskRating: string | null;
  accountType: string;
  createdAt: string;
  verificationDeadline: string;
  assignedTo: string;
  firstApprovedBy: string | null;
  verified: boolean;
  customer: CustomerDetails;
  jointCustomerName: string | null;
  flags: TicketFlag[];
  vendorResponse: VendorResponseDetails | null;
  decision: DecisionDetails | null;
}

const flagLabels: Record<string, string> = {
  VerificationFailed: 'Identity verification failed',
  OfacMatch: 'Name matches the OFAC sanctions list',
  ProhibitedIndustry: 'Occupation or employer is in a prohibited industry (cannabis)',
  ClosedJurisdiction: 'Country of residence is not accepted',
};

const riskCodeLabels: Record<string, string> = {
  DeceasedSSN: 'The tax ID belongs to a deceased person',
  CorrectionalAddress: 'The address is a correctional facility',
};

export function flagLabel(violationType: string): string {
  return flagLabels[violationType] ?? violationType;
}

export function riskCodeLabel(code: string): string {
  return riskCodeLabels[code] ?? code;
}

export function riskCodeList(riskCodes: string): string[] {
  return riskCodes.split(',').map((code) => code.trim()).filter((code) => code.length > 0);
}
